using MESS.Mapping;

namespace MESS.Formats.JMF
{
    public class JmfMap : Map
    {
        public List<string> RecentExportPaths { get; } = new();

        public JmfBackgroundImageSettings? TopViewBackgroundImage { get; set; }
        public JmfBackgroundImageSettings? FrontViewBackgroundImage { get; set; }
        public JmfBackgroundImageSettings? SideViewBackgroundImage { get; set; }
    }
}
