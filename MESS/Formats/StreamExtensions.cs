using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MESS.Formats
{
    static class StreamExtensions
    {
        public static int ReadInt(this Stream stream) => BitConverter.ToInt32(stream.ReadBytes(4), 0);

        public static float ReadFloat(this Stream stream) => BitConverter.ToSingle(stream.ReadBytes(4), 0);

        public static string ReadString(this Stream stream, int length)
        {
            var buffer = stream.ReadBytes(length);
            var actualLength = buffer.TakeWhile(b => b != 0).Count();
            return Encoding.ASCII.GetString(buffer, 0, actualLength);
        }

        public static string ReadNString(this Stream stream)
        {
            var length = stream.ReadByte();
            if (length == -1)
                throw new EndOfStreamException();

            return stream.ReadString(length);
        }

        public static byte[] ReadBytes(this Stream stream, int count)
        {
            var buffer = new byte[count];
            if (stream.Read(buffer, 0, buffer.Length) < count)
                throw new EndOfStreamException();
            return buffer;
        }
    }
}
