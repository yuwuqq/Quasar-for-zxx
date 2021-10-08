using Microsoft.Win32;
using Quasar.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quasar.Client.Extensions
{
    /// <summary>
    /// 为注册表的项和值操作提供扩展。
    /// </summary>
    public static class RegistryKeyExtensions
    {
        /// <summary>
        /// 判断所提供的名称的注册表项是否为空或其值为空。
        /// </summary>
        /// <param name="keyName">与注册表项相关的名称。</param>
        /// <param name="key">实际的注册表项。</param>
        /// <returns>如果提供的名称为空或空，或者项为空，则为真；否则为假。</returns>
        private static bool IsNameOrValueNull(this string keyName, RegistryKey key)
        {
            return (string.IsNullOrEmpty(keyName) || (key == null));
        }

        /// <summary>
        /// 试图使用指定的键名来获取项的字符串值。该方法假设输入正确。
        /// </summary>
        /// <param name="key">我们获得其值的项。</param>
        /// <param name="keyName">项的名称。</param>
        /// <param name="defaultValue">如果数值无法确定，则为默认值。</param>
        /// <returns>使用指定的项名返回键的值。如果无法做到这一点，将返回defaultValue代替。</returns>
        public static string GetValueSafe(this RegistryKey key, string keyName, string defaultValue = "")
        {
            try
            {
                return key.GetValue(keyName, defaultValue).ToString();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 试图从使用指定名称提供的项中获得一个只读（不可写）的子键。
        /// 抛出的异常将被捕获，并且只返回一个空项。
        /// 该方法假定调用者在使用完钥匙后会将其处理掉。
        /// </summary>
        /// <param name="key">获取子项的项。</param>
        /// <param name="name">子项的名称。</param>
        /// <returns>返回从提供的密钥和名称中获得的子密钥；如果无法获得子密钥，则返回空。</returns>
        public static RegistryKey OpenReadonlySubKeySafe(this RegistryKey key, string name)
        {
            try
            {
                return key.OpenSubKey(name, false);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 试图从使用指定名称提供的项中获得一个可写的子项。
        /// 该方法假定调用者在使用完该钥匙后会将其处理掉。
        /// </summary>
        /// <param name="key">获取子项的项。</param>
        /// <param name="name">子项的名称。</param>
        /// <returns>返回从提供的密钥和名称中获得的子密钥；如果无法获得子密钥，则返回空。</returns>
        public static RegistryKey OpenWritableSubKeySafe(this RegistryKey key, string name)
        {
            try
            {
                return key.OpenSubKey(name, true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 试图使用指定的名称从提供的项中创建一个子项。
        /// 该方法假定调用者在使用完该项后会将其处理掉。
        /// </summary>
        /// <param name="key">要创建子项的项。</param>
        /// <param name="name">子项的名称。</param>
        /// <returns>返回为所提供的项和名称创建的子项；如果无法创建子项，则返回空。</returns>
        public static RegistryKey CreateSubKeySafe(this RegistryKey key, string name)
        {
            try
            {
                return key.CreateSubKey(name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 试图从使用指定名称提供的项中删除一个项及其子项。
        /// </summary>
        /// <param name="key">要被删除子项的父项。</param>
        /// <param name="name">子项的名字。</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool DeleteSubKeyTreeSafe(this RegistryKey key, string name)
        {
            try
            {
                key.DeleteSubKeyTree(name, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /*
        * Derived and Adapted from drdandle's article, 
        * Copy and Rename Registry Keys at Code project.
        * Copy and Rename Registry Keys (Post Date: November 11, 2006)
        * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        * 这是一部不属于原作的作品。它已被修改，以适应另一个应用程序的需要。
        * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        * First Modified by StingRaptor on January 21, 2016
        * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        * Original Source:
        * http://www.codeproject.com/Articles/16343/Copy-and-Rename-Registry-Keys
        */

        /// <summary>
        /// 试图使用指定的旧名称和新名称将一个子键重命名为所键名。
        /// </summary>
        /// <param name="key">子键要被重新命名的键名。</param>
        /// <param name="oldName">子键的旧名称.</param>
        /// <param name="newName">子键的新名称.</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool RenameSubKeySafe(this RegistryKey key, string oldName, string newName)
        {
            try
            {
                //从旧的复制到新的
                key.CopyKey(oldName, newName);
                //处置旧键
                key.DeleteSubKeyTree(oldName);
                return true;
            }
            catch
            {
                //尝试处置新键（重命名失败）。
                key.DeleteSubKeyTreeSafe(newName);
                return false;
            }
        }

        /// <summary>
        /// 试图将一个旧的子键复制到所提供的使用指定的旧名称和新名称的父键的新子键上。(抛出异常)
        /// </summary>
        /// <param name="key">子键要被删除的父键。</param>
        /// <param name="oldName">子键的旧名称</param>
        /// <param name="newName">子键的新名称</param>
        public static void CopyKey(this RegistryKey key, string oldName, string newName)
        {
            //Create a new key
            using (RegistryKey newKey = key.CreateSubKey(newName))
            {
                //Open old key
                using (RegistryKey oldKey = key.OpenSubKey(oldName, true))
                {
                    //Copy from old to new
                    RecursiveCopyKey(oldKey, newKey);
                }
            }
        }

        /// <summary>
        /// 试图将一个子键重命名为所提供的使用指定的旧名称和新名称的键。
        /// </summary>
        /// <param name="sourceKey">要复制的源密钥。</param>
        /// <param name="destKey">要复制到的目标键。</param>
        private static void RecursiveCopyKey(RegistryKey sourceKey, RegistryKey destKey)
        {

            //复制所有的注册表值
            foreach (string valueName in sourceKey.GetValueNames())
            {
                object valueObj = sourceKey.GetValue(valueName);
                RegistryValueKind valueKind = sourceKey.GetValueKind(valueName);
                destKey.SetValue(valueName, valueObj, valueKind);
            }

            //复制所有的子键
            foreach (string subKeyName in sourceKey.GetSubKeyNames())
            {
                using (RegistryKey sourceSubkey = sourceKey.OpenSubKey(subKeyName))
                {
                    using (RegistryKey destSubKey = destKey.CreateSubKey(subKeyName))
                    {
                        //递归调用，复制子键数据
                        RecursiveCopyKey(sourceSubkey, destSubKey);
                    }
                }
            }
        }

        /// <summary>
        /// 试图使用指定的名称、数据和种类为提供的键设置一个注册表值。如果注册表的值不存在，它将被创建。
        /// </summary>
        /// <param name="key">要为其设置值的键。</param>
        /// <param name="name">值的名称。</param>
        /// <param name="data">值的数据</param>
        /// <param name="kind">价值的种类</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool SetValueSafe(this RegistryKey key, string name, object data, RegistryValueKind kind)
        {
            try
            {
                // handle type conversion
                if (kind != RegistryValueKind.Binary && data.GetType() == typeof(byte[]))
                {
                    switch (kind)
                    {
                        case RegistryValueKind.String:
                        case RegistryValueKind.ExpandString:
                            data = ByteConverter.ToString((byte[]) data);
                            break;
                        case RegistryValueKind.DWord:
                            data = ByteConverter.ToUInt32((byte[]) data);
                            break;
                        case RegistryValueKind.QWord:
                            data = ByteConverter.ToUInt64((byte[]) data);
                            break;
                        case RegistryValueKind.MultiString:
                            data = ByteConverter.ToStringArray((byte[]) data);
                            break;
                    }
                }
                key.SetValue(name, data, kind);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 试图删除一个使用指定名称提供的键的注册表值。
        /// </summary>
        /// <param name="key">要删除值的键。</param>
        /// <param name="name">值的名称。</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool DeleteValueSafe(this RegistryKey key, string name)
        {
            try
            {
                key.DeleteValue(name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to rename a registry value to the key provided using the specified old
        /// name and new name.
        /// </summary>
        /// <param name="key">The key of which the registry value is to be renamed from.</param>
        /// <param name="oldName">The old name of the registry value.</param>
        /// <param name="newName">The new name of the registry value.</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool RenameValueSafe(this RegistryKey key, string oldName, string newName)
        {
            try
            {
                //Copy from old to new
                key.CopyValue(oldName, newName);
                //Dispose of the old value
                key.DeleteValue(oldName);
                return true;
            }
            catch
            {
                //Try to dispose of the newKey (The rename failed)
                key.DeleteValueSafe(newName);
                return false;
            }
        }

        /// <summary>
        /// Attempts to copy a old registry value to a new registry value for the key 
        /// provided using the specified old name and new name. (throws exceptions)
        /// </summary>
        /// <param name="key">The key of which the registry value is to be copied.</param>
        /// <param name="oldName">The old name of the registry value.</param>
        /// <param name="newName">The new name of the registry value.</param>
        public static void CopyValue(this RegistryKey key, string oldName, string newName)
        {
            RegistryValueKind valueKind = key.GetValueKind(oldName);
            object valueData = key.GetValue(oldName);

            key.SetValue(newName, valueData, valueKind);
        }

        /// <summary>
        /// Checks if the specified subkey exists in the key
        /// </summary>
        /// <param name="key">The key of which to search.</param>
        /// <param name="name">The name of the sub-key to find.</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool ContainsSubKey(this RegistryKey key, string name)
        {
            foreach (string subkey in key.GetSubKeyNames())
            {
                if (subkey == name)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查指定的注册表值是否存在于键中
        /// </summary>
        /// <param name="key">搜索的键。</param>
        /// <param name="name">要查找的注册表值的名称。</param>
        /// <returns>Returns <c>true</c> if the action succeeded, otherwise <c>false</c>.</returns>
        public static bool ContainsValue(this RegistryKey key, string name)
        {
            foreach (string value in key.GetValueNames())
            {
                if (value == name)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取所有与注册表键相关的值名称，并返回过滤后的值的格式化字符串。
        /// </summary>
        /// <param name="key">获得数值的注册表键。</param>
        /// <returns>Yield返回键和键值的格式化字符串。</returns>
        public static IEnumerable<Tuple<string, string>> GetKeyValues(this RegistryKey key)
        {
            if (key == null) yield break;

            foreach (var k in key.GetValueNames().Where(keyVal => !keyVal.IsNameOrValueNull(key)).Where(k => !string.IsNullOrEmpty(k)))
            {
                yield return new Tuple<string, string>(k, key.GetValueSafe(k));
            }
        }

        /// <summary>
        /// 获取一个注册表值的给定数据类型的默认值。
        /// </summary>
        /// <param name="valueKind">注册表值的数据类型。</param>
        /// <returns>给定的默认值 <see cref="valueKind"/>.</returns>
        public static object GetDefault(this RegistryValueKind valueKind)
        {
            switch (valueKind)
            {
                case RegistryValueKind.Binary:
                    return new byte[] {};
                case RegistryValueKind.MultiString:
                    return new string[] {};
                case RegistryValueKind.DWord:
                    return 0;
                case RegistryValueKind.QWord:
                    return (long)0;
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    return "";
                default:
                    return null;
            }
        }
    }
}
