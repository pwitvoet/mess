using HotspotMaker.History;
using MLib.Texturing.Hotspotting;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

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

        public override bool IsModified => base.IsModified || Rectangles.Any(rectangleVM => rectangleVM.IsModified);


        private HotspotRectangleSetVM(UndoSystem undoSystem)
            : base(undoSystem)
        {
            Rectangles.CollectionChanged += Rectangles_CollectionChanged;
        }

        public HotspotRectangleSetVM(string name, UndoSystem undoSystem)
            : this(undoSystem)
        {
            WithoutChangeTracking(() =>
            {
                Name = name;
            });
        }

        public HotspotRectangleSetVM(HotspotRectangleSet rectangleSet, UndoSystem undoSystem)
            : this(undoSystem)
        {
            WithoutChangeTracking(() =>
            {
                Name = rectangleSet.Name;

                foreach (var rectangle in rectangleSet.Rectangles)
                    Rectangles.Add(new HotspotRectangleVM(rectangle, undoSystem));
            });
        }

        public HotspotRectangleSet CreateHotspotRectangleSet()
        {
            var rectangles = Rectangles
                .Select(rectangleVM => rectangleVM.CreateHotspotRectangle())
                .ToArray();

            return new HotspotRectangleSet(Name, rectangles);
        }

        public override void MarkAsUnmodified()
        {
            base.MarkAsUnmodified();

            foreach (var rectangleVM in Rectangles)
                rectangleVM.MarkAsUnmodified();
        }


        private void Rectangles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var rectangleVM in e.NewItems.OfType<HotspotRectangleVM>())
                    rectangleVM.PropertyChanged += RectangleVM_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (var rectangleVM in e.OldItems.OfType<HotspotRectangleVM>())
                    rectangleVM.PropertyChanged -= RectangleVM_PropertyChanged;
            }

            RaisePropertyChanged(nameof(IsModified));
        }

        private void RectangleVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HotspotRectangleVM.IsModified))
                RaisePropertyChanged(nameof(IsModified));
        }
    }
}
