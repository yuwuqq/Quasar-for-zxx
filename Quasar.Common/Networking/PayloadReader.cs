using ProtoBuf;
using Quasar.Common.Messages;
using System;
using System.IO;

namespace Quasar.Common.Networking
{
    public class PayloadReader : MemoryStream
    {
        private readonly Stream _innerStream;
        public bool LeaveInnerStreamOpen { get; }

        public PayloadReader(byte[] payload, int length, bool leaveInnerStreamOpen)
        {
            _innerStream = new MemoryStream(payload, 0, length, false, true);
            LeaveInnerStreamOpen = leaveInnerStreamOpen;
        }

        public PayloadReader(Stream stream, bool leaveInnerStreamOpen)
        {
            _innerStream = stream;
            LeaveInnerStreamOpen = leaveInnerStreamOpen;
        }

        public int ReadInteger()
        {
            return BitConverter.ToInt32(ReadBytes(4), 0);
        }

        public byte[] ReadBytes(int length)
        {
            if (_innerStream.Position + length <= _innerStream.Length)
            {
                byte[] result = new byte[length];
                _innerStream.Read(result, 0, result.Length);
                return result;
            }
            throw new OverflowException($"Unable to read {length} bytes from stream");
        }

        /// <summary>
        /// 读取有效载荷的序列化信息，并将其反序列化。
        /// </summary>
        /// <returns>有效载荷的反序列化信息。</returns>
        public IMessage ReadMessage()
        {
            ReadInteger();
            //长度前缀在这里被忽略了，已经在客户端类中处理了，
            //如果在这里检查分裂或未完全收到的数据包，会造成很大的麻烦。
            IMessage message = Serializer.Deserialize<IMessage>(_innerStream);
            return message;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (LeaveInnerStreamOpen)
                {
                    _innerStream.Flush();
                }
                else
                {
                    _innerStream.Close();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
