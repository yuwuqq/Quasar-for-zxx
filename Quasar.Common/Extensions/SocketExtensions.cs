using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Quasar.Common.Extensions
{
    /// <summary>
    ///KeepAlive的套接字扩展
    /// </summary>
    /// <Author>Abdullah Saleem</Author>
    /// <Email>a.saleem2993@gmail.com</Email>
    public static class SocketExtensions
    {
        /// <summary>
        ///     一个由SetKeepAliveEx方法使用的结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct TcpKeepAlive
        {
            internal uint onoff;
            internal uint keepalivetime;
            internal uint keepaliveinterval;
        };

        /// <summary>
        ///     设置当前tcp连接的Keep-Alive值
        /// </summary>
        /// <param name="socket">当前的套接字实例</param>
        /// <param name="keepAliveInterval">指定当没有收到响应时，TCP重复发送keep-alive的频率。
        /// TCP发送keep-alive传输，以验证空闲的连接仍然是活动的。
        /// 这可以防止TCP在无意中断开活动线路的连接。</param>
        /// <param name="keepAliveTime">指定TCP发送 "保持 "传输的频率。TCP发送keep-alive传输，
        /// 以验证一个空闲的连接仍然是活动的。当远程系统正在响应TCP时，该条目就会被使用。
        /// 否则，传输的间隔由keepAliveInterval条目的值决定。</param>
        public static void SetKeepAliveEx(this Socket socket, uint keepAliveInterval, uint keepAliveTime)
        {
            var keepAlive = new TcpKeepAlive
            {
                onoff = 1,
                keepaliveinterval = keepAliveInterval,
                keepalivetime = keepAliveTime
            };
            int size = Marshal.SizeOf(keepAlive);
            IntPtr keepAlivePtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(keepAlive, keepAlivePtr, true);
            var buffer = new byte[size];
            Marshal.Copy(keepAlivePtr, buffer, 0, size);
            Marshal.FreeHGlobal(keepAlivePtr);
            socket.IOControl(IOControlCode.KeepAliveValues, buffer, null);
        }
    }
}
