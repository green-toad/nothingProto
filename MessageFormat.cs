using System;
using AVcontrol;

namespace Nothing.Message
{
    public class Cat(byte[] content, Cat.Type type)
    {
        public readonly byte[] content = content;
        public readonly Type type = type;

        public enum Type : byte
        {
            Meat = 0,
            FirstConfigurationKey = 2,
            SecondConfigurationKey = 3,
            Target = 1,
        }

        public static byte[] Pack(Cat cat)
        {
            if (cat == null)
                throw new ArgumentNullException(nameof(cat));

            int contentLength = cat.content?.Length ?? 0;
            byte[] result = new byte[1 + 4 + contentLength];

            result[0] = (byte)cat.type;
            ToBinary.LittleEndian<Int32>(contentLength).CopyTo(result, 1);

            if (contentLength > 0)
                Array.Copy(cat.content, 0, result, 5, contentLength);

            return result;
        }
        public static Cat Unpack(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length < 5)
                throw new ArgumentException("Data too short, minimum 5 bytes required.", nameof(data));

            Type type = (Type)data[0];
            int contentLength = FromBinary.LittleEndian<Int32>(data.AsSpan(1, 4));

            if (data.Length < 5 + contentLength)
                throw new ArgumentException($"Data length {data.Length} is less than expected {5 + contentLength}.", nameof(data));

            byte[] content = new byte[contentLength];
            if (contentLength > 0)
                Array.Copy(data, 5, content, 0, contentLength);

            return new Cat(content, type);
        }
    }
}