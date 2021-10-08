using System;
using System.Threading;

namespace Quasar.Client.Utilities
{
    /// <summary>
    /// 一个用户范围内的互斥锁，确保每次只有一个实例在运行。
    /// </summary>
    public class SingleInstanceMutex : IDisposable
    {
        /// <summary>
        /// 用于进程同步的mutex。
        /// </summary>
        private readonly Mutex _appMutex;

        /// <summary>
        /// 代表该互斥锁是在系统上创建的还是已经存在的。
        /// </summary>
        public bool CreatedNew { get; }

        /// <summary>
        /// 确定实例是否被处置，不应该再被使用。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 使用给定的mutex名称初始化一个新的<see cref="SingleInstanceMutex"/>的实例。
        /// </summary>
        /// <param name="name">互斥锁的名称。</param>
        public SingleInstanceMutex(string name)
        {
            _appMutex = new Mutex(false, $"Local\\{name}", out var createdNew);
            CreatedNew = createdNew;
        }

        /// <summary>
        /// 释放这个<see cref="SingleInstanceMutex"/>所使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放mutex对象。
        /// </summary>
        /// <param name="disposing"><c>True</c> 如果从 <see cref="Dispose"/>调用, <c>false</c> 如果从finalizer那里调用的话。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                _appMutex?.Dispose();
            }

            IsDisposed = true;
        }
    }
}
