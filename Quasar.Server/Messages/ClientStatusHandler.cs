using Quasar.Common.Enums;
using Quasar.Common.Messages;
using Quasar.Common.Networking;
using Quasar.Server.Networking;

namespace Quasar.Server.Messages
{
    /// <summary>
    /// 处理与远程客户状态互动的消息。
    /// </summary>
    public class ClientStatusHandler : MessageProcessorBase<object>
    {
        /// <summary>
        /// 代表将处理状态更新的方法。
        /// </summary>
        /// <param name="sender">引起该事件的消息处理程序。</param>
        /// <param name="client">更新状态的客户。</param>
        /// <param name="statusMessage">新的地位。</param>
        public delegate void StatusUpdatedEventHandler(object sender, Client client, string statusMessage);

        /// <summary>
        /// 代表将处理用户状态更新的方法。
        /// </summary>
        /// <param name="sender">引起该事件的消息处理程序。</param>
        /// <param name="client">更新用户状态的客户端。</param>
        /// <param name="userStatusMessage">新的用户状态。</param>
        public delegate void UserStatusUpdatedEventHandler(object sender, Client client, UserStatus userStatusMessage);

        /// <summary>
        /// 当一个客户更新其状态时引发的。
        /// </summary>
        /// <remarks>
        /// 用此事件注册的处理程序将在构建实例时选择的
        /// <see cref="System.Threading.SynchronizationContext"/>上调用。
        /// </remarks>
        public event StatusUpdatedEventHandler StatusUpdated;

        /// <summary>
        /// 当一个客户更新其用户状态时引发的。
        /// </summary>
        /// <remarks>
        /// 用此事件注册的处理程序将在构建实例时选择的<see cref="System.Threading.SynchronizationContext"/>上调用。
        /// </remarks>
        public event UserStatusUpdatedEventHandler UserStatusUpdated;

        /// <summary>
        /// 报告更新的状态。
        /// </summary>
        /// <param name="client">更新状态的客户端。</param>
        /// <param name="statusMessage">新的状态。</param>
        private void OnStatusUpdated(Client client, string statusMessage)
        {
            SynchronizationContext.Post(c =>
            {
                var handler = StatusUpdated;
                handler?.Invoke(this, (Client) c, statusMessage);
            }, client);
        }

        /// <summary>
        /// 报告一个更新的用户状态。
        /// </summary>
        /// <param name="client">更新用户状态的客户端。</param>
        /// <param name="userStatusMessage">新的用户状态。</param>
        private void OnUserStatusUpdated(Client client, UserStatus userStatusMessage)
        {
            SynchronizationContext.Post(c =>
            {
                var handler = UserStatusUpdated;
                handler?.Invoke(this, (Client) c, userStatusMessage);
            }, client);
        }

        /// <summary>
        /// 初始化一个<see cref="ClientStatusHandler"/>类的新实例。
        /// </summary>
        public ClientStatusHandler() : base(true)
        {
        }

        /// <inheritdoc />
        public override bool CanExecute(IMessage message) => message is SetStatus || message is SetUserStatus;

        /// <inheritdoc />
        public override bool CanExecuteFrom(ISender sender) => true;

        /// <inheritdoc />
        public override void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case SetStatus status:
                    Execute((Client) sender, status);
                    break;
                case SetUserStatus userStatus:
                    Execute((Client) sender, userStatus);
                    break;
            }
        }

        private void Execute(Client client, SetStatus message)
        {
            OnStatusUpdated(client, message.Message);
        }

        private void Execute(Client client, SetUserStatus message)
        {
            OnUserStatusUpdated(client, message.Message);
        }
    }
}
