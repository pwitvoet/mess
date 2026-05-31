using MLib.Texturing.Hotspotting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HotspotMaker.Hotspot
{
    // TODO: Add Note/Description properties to bindings and rectangles! For editor purposes, so users can make notes (TODO's, etc).
    public class HotspotFile
    {
        public string FilePath { get; }
        public List<HotspotRectangleSetVM> HotspotRectangleSets { get; }
        public List<HotspotBindingVM> HotspotBindings { get; }


        public HotspotFile(string filePath, IEnumerable<HotspotRectangleSetVM> hotspotRectangleSets, IEnumerable<HotspotBindingVM> hotspotBindings)
        {
            FilePath = filePath;
            HotspotRectangleSets = hotspotRectangleSets.ToList();
            HotspotBindings = hotspotBindings.ToList();
        }


        public static HotspotFile Load(string filePath)
        {
            using (var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hotspotFileData = HotspotFileParser.Parse(file);

                var rectangleSets = hotspotFileData.RectangleSets
                    .Select(rectangleSet => new HotspotRectangleSetVM(rectangleSet))
                    .ToArray();

                var bindings = hotspotFileData.Bindings
                    .Select(binding => new HotspotBindingVM(binding))
                    .ToArray();

                return new HotspotFile(filePath, rectangleSets, bindings);
            }
        }
    }
}
