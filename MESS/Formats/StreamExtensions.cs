using System.Text;

namespace MESS.Formats
{
    static class StreamExtensions
    {
        // Reading:
        public static short ReadShort(this Stream stream) => BitConverter.ToInt16(stream.ReadBytes(2), 0);

        public static int ReadInt(this Stream stream) => BitConverter.ToInt32(stream.ReadBytes(4), 0);

        public static uint ReadUint(this Stream stream) => BitConverter.ToUInt32(stream.ReadBytes(4), 0);

        public static float ReadFloat(this Stream stream) => BitConverter.ToSingle(stream.ReadBytes(4), 0);

        public static double ReadDouble(this Stream stream) => BitConverter.ToDouble(stream.ReadBytes(8), 0);

        /// <summary>
        /// Reads a fixed-length ASCII string.
        /// </summary>
        public static string ReadFixedLengthString(this Stream stream, int length, Encoding? encoding = null)
        {
            var buffer = stream.ReadBytes(length);
            var actualLength = buffer.TakeWhile(b => b != 0).Count();
            return (encoding ?? Encoding.ASCII).GetString(buffer, 0, actualLength);
        }

        /// <summary>
        /// Reads a length-prefixed ASCII string, where the length takes up 1 byte.
        /// </summary>
        public static string ReadNString(this Stream stream, Encoding? encoding = null)
        {
            var length = stream.ReadByte();
            if (length == -1)
                throw new EndOfStreamException();

            return stream.ReadFixedLengthString(length, encoding);
        }

        /// <summary>
        /// Reads a length-prefixed UTF-8 string, where the length is a 32-bit integer.
        /// </summary>
        public static string? ReadLengthPrefixedString(this Stream stream, Encoding? encoding = null)
        {
            var length = stream.ReadInt();
            if (length == -1)
                return null;

            return (encoding ?? Encoding.UTF8).GetString(stream.ReadBytes(length));
        }

        public static byte ReadSingleByte(this Stream stream)
        {
            var value = stream.ReadByte();
            if (value == -1)
                throw new EndOfStreamException();
            return (byte)value;
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


        // Writing:
        public static void WriteShort(this Stream stream, short value) => stream.Write(BitConverter.GetBytes(value), 0, 2);

        public static void WriteInt(this Stream stream, int value) => stream.Write(BitConverter.GetBytes(value), 0, 4);

        public static void WriteFloat(this Stream stream, float value) => stream.Write(BitConverter.GetBytes(value), 0, 4);

        public static void WriteDouble(this Stream stream, double value) => stream.Write(BitConverter.GetBytes(value), 0, 8);

        /// <summary>
        /// Writes a fixed-length ASCII string. Values that are too long will be truncated.
        /// </summary>
        public static void WriteFixedLengthString(this Stream stream, string value, int? length = null, Encoding? encoding = null)
        {
            var buffer = (encoding ?? Encoding.ASCII).GetBytes(value);
            if (length == null)
            {
                stream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                stream.Write(buffer, 0, Math.Min(buffer.Length, length.Value));
                if (buffer.Length < length.Value)
                    stream.Write(new byte[length.Value - buffer.Length], 0, length.Value - buffer.Length);
            }
        }

        /// <summary>
        /// Writes a length-prefixed ASCII string, where the length takes up 1 byte.
        /// Throws an exception if the value is too long, unless the <paramref name="truncate"/> parameter is true.
        /// </summary>
        public static void WriteNString(this Stream stream, string value, bool addNullTerminator = true, Encoding? encoding = null, bool truncate = false)
        {
            if (addNullTerminator && value.Length == 0 || value[value.Length - 1] != '\0')
                value += '\0';

            var bytes = (encoding ?? Encoding.ASCII).GetBytes(value);
            var length = bytes.Length;
            if (length > 255)
            {
                if (!truncate)
                    throw new InvalidDataException("An NString can only contain up to 255 characters.");

                bytes[254] = 0;
                length = 255;
            }

            stream.WriteByte((byte)length);
            stream.Write(bytes, 0, length);
        }

        /// <summary>
        /// Writes a length-prefixed UTF-8 string, where the length is a 32-bit integer.
        /// </summary>
        public static void WriteLengthPrefixedString(this Stream stream, string? value, Encoding? encoding = null)
        {
            if (value == null)
            {
                stream.WriteInt(-1);
                return;
            }

            var bytes = (encoding ?? Encoding.UTF8).GetBytes(value);
            stream.WriteInt(bytes.Length);
            stream.Write(bytes);
        }

        public static void WriteBytes(this Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
    }
}
