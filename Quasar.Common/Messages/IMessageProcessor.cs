using Quasar.Common.Networking;

namespace Quasar.Common.Messages
{
    /// <summary>
    /// 提供处理信息的基本功能。
    /// </summary>
    public interface IMessageProcessor
    {
        /// <summary>
        /// 决定这个消息处理器是否可以执行指定的消息。
        /// </summary>
        /// <param name="message">要执行的信息。</param>
        /// <returns><c>True</c> 如果该信息可以被这个信息处理器执行，否则 <c>false</c>.</returns>
        bool CanExecute(IMessage message);

        /// <summary>
        /// 决定该消息处理器是否可以执行从发送方收到的消息。
        /// </summary>
        /// <param name="sender">信息的发件人。</param>
        /// <returns><c>True</c> 如果这个消息处理器可以执行来自发件人的消息，否则 <c>false</c>.</returns>
        bool CanExecuteFrom(ISender sender);

        /// <summary>
        /// 执行收到的信息。
        /// </summary>
        /// <param name="sender">该信息的发件人。</param>
        /// <param name="message">收到的信息。</param>
        void Execute(ISender sender, IMessage message);
    }
}
