using Quasar.Client.Helper;
using Quasar.Client.Networking;
using Quasar.Common.Enums;
using Quasar.Common.Messages;
using System;
using System.Threading;

namespace Quasar.Client.User
{
    /// <summary>
    /// 提供用户活动检测并在变化时发送<see cref="SetUserStatus"/>消息。
    /// </summary>
    public class ActivityDetection : IDisposable
    {
        /// <summary>
        /// 存储最后的用户状态以检测变化。
        /// </summary>
        private UserStatus _lastUserStatus;

        /// <summary>
        /// 用来与服务器通信的客户端。
        /// </summary>
        private readonly QuasarClient _client;

        /// <summary>
        /// 创建一个<see cref="_token"/>和信号取消。
        /// </summary>
        private readonly CancellationTokenSource _tokenSource;

        /// <summary>
        /// 要检查取消的令牌。
        /// </summary>
        private readonly CancellationToken _token;

        /// <summary>
        /// 使用给定的客户端初始化一个新的<see cref="ActivityDetection"/>的实例。
        /// </summary>
        /// <param name="client">互斥锁的名称。</param>
        public ActivityDetection(QuasarClient client)
        {
            _client = client;
            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
            client.ClientState += OnClientStateChange;
        }

        private void OnClientStateChange(Networking.Client s, bool connected)
        {
            // 重置用户状态
            if (connected)
                _lastUserStatus = UserStatus.Active;
        }

        /// <summary>
        /// 启动用户活动检测。
        /// </summary>
        public void Start()
        {
            new Thread(UserActivityThread).Start();
        }

        /// <summary>
        /// 检查用户活动变化，在变化时向<see cref="SetUserStatus"/>发送<see cref="_client"/>。
        /// </summary>
        private void UserActivityThread()
        {
            while (!_token.WaitHandle.WaitOne(10))
            {
                if (IsUserIdle())
                {
                    if (_lastUserStatus != UserStatus.Idle)
                    {
                        _lastUserStatus = UserStatus.Idle;
                        _client.Send(new SetUserStatus {Message = _lastUserStatus});
                    }
                }
                else
                {
                    if (_lastUserStatus != UserStatus.Active)
                    {
                        _lastUserStatus = UserStatus.Active;
                        _client.Send(new SetUserStatus {Message = _lastUserStatus});
                    }
                }
            }
        }

        /// <summary>
        /// 如果用户最后一次输入的时间超过10分钟，则确定用户是否处于空闲状态。
        /// </summary>
        /// <returns><c>True</c> 如果用户是空闲的，否则 <c>false</c>.</returns>
        private bool IsUserIdle()
        {
            var ticks = Environment.TickCount;

            var idleTime = ticks - NativeMethodsHelper.GetLastInputInfoTickCount();

            idleTime = ((idleTime > 0) ? (idleTime / 1000) : 0);

            return (idleTime > 600); // idle for 10 minutes
        }

        /// <summary>
        /// 处置与该活动检测服务相关的所有管理和非管理资源。
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
                _client.ClientState -= OnClientStateChange;
                _tokenSource.Cancel();
                _tokenSource.Dispose();
            }
        }
    }
}
