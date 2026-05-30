namespace MLib.Texturing
{
    public enum TextureType
    {
        SimpleTexture =     0x42,
        MipmapTexture =     0x43,
        Wad2MipTexture =    0x44,
        Font =              0x46,
    }


    public class TextureInfo
    {
        public string Name { get; }
        public TextureType Type { get; }
        public int Width { get; }
        public int Height { get; }

        internal int FileOffset { get; }


        internal TextureInfo(string name, TextureType type, int width, int height, int fileOffset)
        {
            Name = name;
            Type = type;
            Width = width;
            Height = height;

            FileOffset = fileOffset;
        }
    }
}
