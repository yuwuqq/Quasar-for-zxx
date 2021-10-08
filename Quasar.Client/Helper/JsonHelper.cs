using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Quasar.Client.Helper
{
    /// <summary>
    /// 提供对JSON进行序列化和反序列化的方法。
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Serializes an object to the respectable JSON string.
        /// </summary>
        public static string Serialize<T>(T o)
        {
            var s = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                s.WriteObject(ms, o);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// 将一个JSON字符串反序列化为指定的对象。
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            var s = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)s.ReadObject(ms);
            }
        }

        /// <summary>
        /// 将一个JSON流反序列化为指定的对象。
        /// </summary>
        public static T Deserialize<T>(Stream stream)
        {
            var s = new DataContractJsonSerializer(typeof(T)); 
            return (T)s.ReadObject(stream);
        }
    }
}
