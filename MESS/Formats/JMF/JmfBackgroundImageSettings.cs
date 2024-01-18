namespace MESS.Formats.JMF
{
    public class JmfBackgroundImageSettings
    {
        public string ImagePath { get; set; } = "";
        public double Scale { get; set; }
        public int Luminance { get; set; }
        public ImageFiltering Filtering { get; set; }
        public bool InvertColors { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public byte[]? UnknownData { get; set; }
    }

    public enum ImageFiltering
    {
        Nearest = 0,
        Linear = 1,
    }
}
