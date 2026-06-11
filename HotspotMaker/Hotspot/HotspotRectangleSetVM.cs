using HotspotMaker.History;
using MLib.Texturing.Hotspotting;
using System.Collections.ObjectModel;

namespace HotspotMaker.Hotspot
{
    public class HotspotRectangleSetVM : ChangeTrackingVM
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetPropertyOngoing(v => _name = v, _name, value);
        }

        public ObservableCollection<HotspotRectangleVM> Rectangles { get; } = new();


        public HotspotRectangleSetVM(string name, UndoSystem undoSystem)
            : base(undoSystem)
        {
            WithoutChangeTracking(() =>
            {
                Name = name;
            });
        }

        public HotspotRectangleSetVM(HotspotRectangleSet rectangleSet, UndoSystem undoSystem)
            : base(undoSystem)
        {
            WithoutChangeTracking(() =>
            {
                Name = rectangleSet.Name;

                foreach (var rectangle in rectangleSet.Rectangles)
                    Rectangles.Add(new HotspotRectangleVM(rectangle, undoSystem));
            });
        }
    }
}
