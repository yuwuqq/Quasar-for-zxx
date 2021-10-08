using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Quasar.Common.DNS
{
    public class HostsManager
    {
        public bool IsEmpty => _hosts.Count == 0;

        private readonly Queue<Host> _hosts = new Queue<Host>();

        public HostsManager(List<Host> hosts)
        {
            foreach(var host in hosts)
                _hosts.Enqueue(host);
        }

        public Host GetNextHost()
        {
            var temp = _hosts.Dequeue();
            _hosts.Enqueue(temp); // 添加到队列的末端

            temp.IpAddress = ResolveHostname(temp);
            return temp;
        }

        private static IPAddress ResolveHostname(Host host)
        {
            if (string.IsNullOrEmpty(host.Hostname)) return null;

            IPAddress ip;
            if (IPAddress.TryParse(host.Hostname, out ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (!Socket.OSSupportsIPv6) return null;
                }
                return ip;
            }

            var ipAddresses = Dns.GetHostEntry(host.Hostname).AddressList;
            foreach (IPAddress ipAddress in ipAddresses)
            {
                switch (ipAddress.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        return ipAddress;
                    case AddressFamily.InterNetworkV6:
                        // 只有在没有IPv4地址的情况下才使用已解决的IPv6，
                        // 否则有可能是客户用来连接互联网的路由器不支持IPv6。
                        if (ipAddresses.Length == 1)
                            return ipAddress;
                        break;
                }
            }

            return ip;
        }
    }
}
