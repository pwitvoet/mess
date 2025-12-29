using MESS.Mapping;

namespace MESS.Formats.JMF
{
    public class JmfMap : Map
    {
        public List<string> RecentExportPaths { get; } = new();

        public JmfBackgroundImageSettings? TopViewBackgroundImage { get; set; }
        public JmfBackgroundImageSettings? FrontViewBackgroundImage { get; set; }
        public JmfBackgroundImageSettings? SideViewBackgroundImage { get; set; }


        public override Map PartialCopy()
        {
            var copy = new JmfMap();
            PartialCopyTo(copy);

            copy.RecentExportPaths.AddRange(RecentExportPaths);
            copy.TopViewBackgroundImage = TopViewBackgroundImage?.Copy();
            copy.FrontViewBackgroundImage = FrontViewBackgroundImage?.Copy();
            copy.SideViewBackgroundImage = SideViewBackgroundImage?.Copy();
            return copy;
        }
    }
}
