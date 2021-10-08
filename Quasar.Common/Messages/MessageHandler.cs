using Quasar.Common.Networking;
using System.Collections.Generic;
using System.Linq;

namespace Quasar.Common.Messages
{
    /// <summary>
    /// 处理<see cref="IMessageProcessor"/>s的注册和<see cref="IMessage"/>s的处理。
    /// </summary>
    public static class MessageHandler
    {
        /// <summary>
        /// 注册的<see cref="IMessageProcessor"/>s的列表。
        /// </summary>
        private static readonly List<IMessageProcessor> Processors = new List<IMessageProcessor>();

        /// <summary>
        /// 在锁语句中用于同步线程之间对<see cref="Processors"/>的访问。
        /// </summary>
        private static readonly object SyncLock = new object();

        /// <summary>
        /// 将一个<see cref="IMessageProcessor"/>注册到可用的<see cref="Processors"/>。
        /// </summary>
        /// <param name="proc">要注册的<see cref="IMessageProcessor"/>。</param>
        public static void Register(IMessageProcessor proc)
        {
            lock (SyncLock)
            {
                if (Processors.Contains(proc)) return;
                Processors.Add(proc);
            }
        }

        /// <summary>
        /// 从可用的<see cref="IMessageProcessor"/>中取消注册一个<see cref="Processors"/>。
        /// </summary>
        /// <param name="proc"></param>
        public static void Unregister(IMessageProcessor proc)
        {
            lock (SyncLock)
            {
                Processors.Remove(proc);
            }
        }

        /// <summary>
        /// 将收到的<see cref="IMessage"/>转发给适当的<see cref="IMessageProcessor"/>s来执行它。
        /// </summary>
        /// <param name="sender">信息的发件人。</param>
        /// <param name="msg">收到的信息。</param>
        public static void Process(ISender sender, IMessage msg)
        {
            IEnumerable<IMessageProcessor> availableProcessors;
            lock (SyncLock)
            {
                // 选择适当的信息处理程序
                availableProcessors = Processors.Where(x => x.CanExecute(msg) && x.CanExecuteFrom(sender)).ToList();
                // ToList()被要求检索一个线程安全的枚举器，代表消息处理器的时间快照。
            }

            foreach (var executor in availableProcessors)
                executor.Execute(sender, msg);
        }
    }
}
