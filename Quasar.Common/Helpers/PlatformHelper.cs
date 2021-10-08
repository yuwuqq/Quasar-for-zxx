using System;
using System.Management;
using System.Text.RegularExpressions;

namespace Quasar.Common.Helpers
{
    public static class PlatformHelper
    {
        /// <summary>
        /// 初始化<see cref="PlatformHelper"/>类。
        /// </summary>
        static PlatformHelper()
        {
            Win32NT = Environment.OSVersion.Platform == PlatformID.Win32NT;
            XpOrHigher = Win32NT && Environment.OSVersion.Version.Major >= 5;
            VistaOrHigher = Win32NT && Environment.OSVersion.Version.Major >= 6;
            SevenOrHigher = Win32NT && (Environment.OSVersion.Version >= new Version(6, 1));
            EightOrHigher = Win32NT && (Environment.OSVersion.Version >= new Version(6, 2, 9200));
            EightPointOneOrHigher = Win32NT && (Environment.OSVersion.Version >= new Version(6, 3));
            TenOrHigher = Win32NT && (Environment.OSVersion.Version >= new Version(10, 0));
            RunningOnMono = Type.GetType("Mono.Runtime") != null;

            Name = "Unknown OS";
            using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject os in searcher.Get())
                {
                    Name = os["Caption"].ToString();
                    break;
                }
            }

            Name = Regex.Replace(Name, "^.*(?=Windows)", "").TrimEnd().TrimStart(); // 删除第一个匹配 "Windows "之前的所有内容，并修剪末端和起始部分
            Is64Bit = Environment.Is64BitOperatingSystem;
            FullName = $"{Name} {(Is64Bit ? 64 : 32)} Bit";
        }

        /// <summary>
        /// 获取该计算机上运行的操作系统的全称（包括版本和架构）。
        /// </summary>
        public static string FullName { get; }

        /// <summary>
        /// 获取该计算机上运行的操作系统的名称（包括版本）。
        /// </summary>
        public static string Name { get; }

        /// <summary>
        /// 确定操作系统是32位还是64位。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是64位，否则 <c>false</c>表示为32位.
        /// </value>
        public static bool Is64Bit { get; }

        /// <summary>
        /// 返回一个表示应用程序是否在Mono运行时运行的信息。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果应用程序在Mono运行时运行；否则， <c>false</c>.
        /// </value>
        public static bool RunningOnMono { get; }

        /// <summary>
        /// 返回一个表示操作系统是否基于Windows 32 NT。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是基于Windows 32 NT；否则。 <c>false</c>.
        /// </value>
        public static bool Win32NT { get; }

        /// <summary>
        /// 返回一个表示操作系统是否为Windows XP或更高的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是Windows XP或更高版本；否则。<c>false</c>.
        /// </value>
        public static bool XpOrHigher { get; }

        /// <summary>
        /// 返回一个表示操作系统是否为Windows Vista或更高的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是Windows Vista或更高版本；否则。<c>false</c>.
        /// </value>
        public static bool VistaOrHigher { get; }

        /// <summary>
        /// 返回一个表示操作系统是否为Windows 7或更高的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是Windows 7或更高版本；否则。<c>false</c>.
        /// </value>
        public static bool SevenOrHigher { get; }

        /// <summary>
        /// 返回一个表示操作系统是否为Windows 8或更高的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是Windows 8或更高版本；否则。 <c>false</c>.
        /// </value>
        public static bool EightOrHigher { get; }

        /// <summary>
        /// 返回一个表示操作系统是否为Windows 8.1或更高的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是Windows 8.1或更高版本；否则。<c>false</c>.
        /// </value>
        public static bool EightPointOneOrHigher { get; }

        /// <summary>
        /// 返回一个表示操作系统是否为Windows 10或更高的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> 如果操作系统是Windows 10或更高版本；否则。<c>false</c>.
        /// </value>
        public static bool TenOrHigher { get; }
    }
}
