namespace MLib.Texturing
{
    public class Texture
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        public byte[] ImageData { get; }
        public ColorRGB[] Palette { get; }


        public Texture(string name, int width, int height, byte[] imageData, ColorRGB[] palette)
        {
            Name = name;
            Width = width;
            Height = height;
            ImageData = imageData;
            Palette = palette;
        }
    }
}
