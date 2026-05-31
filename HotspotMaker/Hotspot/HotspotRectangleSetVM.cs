using MLib.Texturing.Hotspotting;
using System.Collections.Generic;
using System.Linq;

namespace HotspotMaker.Hotspot
{
    public class HotspotRectangleSetVM
    {
        public string Name { get; set; } = "";
        public List<HotspotRectangleVM> Rectangles { get; } = new();


        public HotspotRectangleSetVM()
        {
        }

        public HotspotRectangleSetVM(HotspotRectangleSet rectangleSet)
        {
            Name = rectangleSet.Name;
            Rectangles.AddRange(rectangleSet.Rectangles.Select(rectangle => new HotspotRectangleVM(rectangle)));
        }
    }
}
