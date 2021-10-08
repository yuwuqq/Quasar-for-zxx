using System.Net;

namespace Quasar.Common.DNS
{
    public class Host
    {
        /// <summary>
        /// 存储主机名称。
        /// </summary>
        /// <remarks>
        /// 可以是一个IPv4、IPv6地址或主机名。
        /// </remarks>
        public string Hostname { get; set; }

        /// <summary>
        /// 存储主机的IP地址。
        /// </summary>
        /// <remarks>
        /// 可以是一个IPv4或IPv6地址。
        /// </remarks>
        public IPAddress IpAddress { get; set; }

        /// <summary>
        /// 存储主机的端口。
        /// </summary>
        public ushort Port { get; set; }

        public override string ToString()
        {
            return Hostname + ":" + Port;
        }
    }
}
