using Quasar.Common.Models;
using System.Collections.Generic;

namespace Quasar.Client.Recovery
{
    /// <summary>
    /// 提供了一种从应用程序读取存储账户的通用方法。
    /// </summary>
    public interface IAccountReader
    {
        /// <summary>
        /// 读取应用程序的存储账户。
        /// </summary>
        /// <returns>恢复的账户列表</returns>
        IEnumerable<RecoveredAccount> ReadAccounts();

        /// <summary>
        /// 应用程序的名称。
        /// </summary>
        string ApplicationName { get; }
    }
}
