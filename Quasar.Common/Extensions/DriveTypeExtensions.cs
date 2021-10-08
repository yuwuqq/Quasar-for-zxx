using System.IO;

namespace Quasar.Common.Extensions
{
    public static class DriveTypeExtensions
    {
        /// <summary>
        /// 将<see cref="DriveType"/>实例的值转换为其友好的字符串表示。
        /// </summary>
        /// <param name="type">The <see cref="DriveType"/>.</param>
        /// <returns>这个<see cref="DriveType"/>实例的值的友好字符串表示。
        public static string ToFriendlyString(this DriveType type)
        {
            switch (type)
            {
                case DriveType.Fixed:
                    return "Local Disk";
                case DriveType.Network:
                    return "Network Drive";
                case DriveType.Removable:
                    return "Removable Drive";
                default:
                    return type.ToString();
            }
        }
    }
}
