using Quasar.Common.Networking;
using System;
using System.Threading;

namespace Quasar.Common.Messages
{
    /// <summary>
    /// 提供一个MessageProcessor实现，提供进度报告的回调。
    /// </summary>
    /// <typeparam name="T">指定进度报告值的类型。</typeparam>
    /// <remarks>
    /// 任何用<see cref="ProgressChanged"/>事件注册的事件处理程序都是通过构建实例时
    /// 选择的<see cref="System.Threading.SynchronizationContext"/>实例进行调用的。
    /// </remarks>
    public abstract class MessageProcessorBase<T> : IMessageProcessor, IProgress<T>
    {
        /// <summary>
        /// 构建时选择的同步环境。
        /// </summary>
        protected readonly SynchronizationContext SynchronizationContext;

        /// <summary>
        /// 一个缓存的委托，用于向同步上下文发布调用。
        /// </summary>
        private readonly SendOrPostCallback _invokeReportProgressHandlers;

        /// <summary>
        /// 代表将处理进度更新的方法。
        /// </summary>
        /// <param name="sender">更新进度的消息处理器。</param>
        /// <param name="value">The new progress.</param>
        public delegate void ReportProgressEventHandler(object sender, T value);

        /// <summary>
        /// 为了每个被报告的程序的值而提出。
        /// </summary>
        /// <remarks>
        /// 用此事件注册的处理程序将在构建实例时选择的<see cref="System.Threading.SynchronizationContext"/>上调用。
        /// </remarks>
        public event ReportProgressEventHandler ProgressChanged;

        /// <summary>
        /// 报告一个进度变化。
        /// </summary>
        /// <param name="value">更新进度的值。</param>
        protected virtual void OnReport(T value)
        {
            // 如果没有处理程序，就不要费心去翻阅同步上下文。
            // 在回调中，我们需要再次检查，以防止在这段时间内有事件处理程序被删除。
            var handler = ProgressChanged;
            if (handler != null)
            {
                SynchronizationContext.Post(_invokeReportProgressHandlers, value);
            }
        }

        /// <summary>
        /// 初始化<see cref="MessageProcessorBase{T}"/>。
        /// </summary>
        /// <param name="useCurrentContext">
        /// 如果这个值是<c>false</c>，进度回调将在ThreadPool上被调用。
        /// 否则将使用当前的SynchronizationContext。
        /// </param>
        protected MessageProcessorBase(bool useCurrentContext)
        {
            _invokeReportProgressHandlers = InvokeReportProgressHandlers;
            SynchronizationContext = useCurrentContext ? SynchronizationContext.Current : ProgressStatics.DefaultContext;
        }

        /// <summary>
        /// 调用进度事件的回调。
        /// </summary>
        /// <param name="state">进度值。</param>
        private void InvokeReportProgressHandlers(object state)
        {
            var handler = ProgressChanged;
            handler?.Invoke(this, (T)state);
        }

        /// <inheritdoc />
        public abstract bool CanExecute(IMessage message);

        /// <inheritdoc />
        public abstract bool CanExecuteFrom(ISender sender);

        /// <inheritdoc />
        public abstract void Execute(ISender sender, IMessage message);

        void IProgress<T>.Report(T value) => OnReport(value);
    }

    /// <summary>
    /// 保存<see cref="MessageProcessorBase{T}"/>的静态值。
    /// </summary>
    /// <remarks>
    /// 这就避免了每个类型T有一个静态实例。
    /// </remarks>
    internal static class ProgressStatics
    {
        /// <summary>
        /// 一个针对<see cref="ThreadPool"/>的默认同步环境。
        /// </summary>
        internal static readonly SynchronizationContext DefaultContext = new SynchronizationContext();
    }
}
