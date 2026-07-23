using System.Buffers;

namespace ClientSide
{
    public class Frame
    {
        public Type type;
        public byte[] content;

        public enum Type : byte
        {
            content = 0,
            firstInitalizeStep = 1,
            secondInitializationStep = 2,
        }

        public static byte[] Pack(Frame frame)
        {
            byte[] content = new byte[1 + frame.content.Length];
            content[0] = (byte)frame.type;
            Buffer.BlockCopy(frame.content, 0, content, 1, frame.content.Length);
            return content;
        }
        public static Frame Unpack(byte[] content)
        {
            var frame = new Frame();
            frame.type = (Type)content[0];
            frame.content = new byte[content.Length - 1];
            Buffer.BlockCopy(content, 1, frame.content, 0, content.Length - 1);
            return frame;
        }
    }
}