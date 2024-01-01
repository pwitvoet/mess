using System.Text;

namespace MESS.Formats
{
    static class StreamExtensions
    {
        public static short ReadShort(this Stream stream) => BitConverter.ToInt16(stream.ReadBytes(2), 0);

        public static int ReadInt(this Stream stream) => BitConverter.ToInt32(stream.ReadBytes(4), 0);

        public static float ReadFloat(this Stream stream) => BitConverter.ToSingle(stream.ReadBytes(4), 0);

        public static double ReadDouble(this Stream stream) => BitConverter.ToDouble(stream.ReadBytes(8), 0);

        /// <summary>
        /// Reads a fixed-length ASCII string.
        /// </summary>
        public static string ReadString(this Stream stream, int length)
        {
            var buffer = stream.ReadBytes(length);
            var actualLength = buffer.TakeWhile(b => b != 0).Count();
            return Encoding.ASCII.GetString(buffer, 0, actualLength);
        }

        /// <summary>
        /// Reads a length-prefixed ASCII string, where the length takes up 1 byte.
        /// </summary>
        public static string ReadNString(this Stream stream)
        {
            var length = stream.ReadByte();
            if (length == -1)
                throw new EndOfStreamException();

            return stream.ReadString(length);
        }

        /// <summary>
        /// Reads a length-prefixed string, where the length is a 32-bit integer.
        /// </summary>
        public static string? ReadLengthPrefixedString(this Stream stream, Encoding? encoding = null)
        {
            var length = stream.ReadInt();
            if (length == -1)
                return null;

            return (encoding ?? Encoding.UTF8).GetString(stream.ReadBytes(length));
        }

        /// <summary>
        /// Reads the specified number of bytes.
        /// Throws <see cref="EndOfStreamException"/> if the stream does not contain sufficient bytes.
        /// </summary>
        public static byte[] ReadBytes(this Stream stream, int count)
        {
            var buffer = new byte[count];
            if (stream.Read(buffer, 0, buffer.Length) < count)
                throw new EndOfStreamException();
            return buffer;
        }
    }
}
