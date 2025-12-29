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


        public JmfBackgroundImageSettings Copy()
        {
            return new JmfBackgroundImageSettings {
                ImagePath = ImagePath,
                Scale = Scale,
                Luminance = Luminance,
                Filtering = Filtering,
                InvertColors = InvertColors,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                UnknownData = UnknownData,
            };
        }
    }

    public enum ImageFiltering
    {
        Nearest = 0,
        Linear = 1,
    }
}
