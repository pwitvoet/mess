using HotspotMaker.History;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace HotspotMaker.Hotspot
{
    public class HotspotRectangleSelectionVM : ChangeTrackingVM
    {
        public event Action<HotspotRectangleVM[], HotspotRectangleVM[]>? SelectionChanged;
        protected void RaiseSelectionChanged(HotspotRectangleVM[] deselected, HotspotRectangleVM[] selected)
            => SelectionChanged?.Invoke(deselected, selected);


        private ObservableCollection<HotspotRectangleVM> _rectangles = new();
        public IEnumerable<HotspotRectangleVM> Rectangles => _rectangles;


        private bool SuppressSelectionChangedEvents { get; set; }


        public HotspotRectangleSelectionVM(UndoSystem undoSystem)
            : base(undoSystem)
        {
            _rectangles.CollectionChanged += Rectangles_CollectionChanged;
        }

        public void Clear()
        {
            _rectangles.Clear();
        }

        public void Add(HotspotRectangleVM rectangleVM)
        {
            _rectangles.Add(rectangleVM);
        }

        public void Add(IEnumerable<HotspotRectangleVM> rectangleVMs)
        {
            var rectangleVMsArray = rectangleVMs.ToArray();

            try
            {
                SuppressSelectionChangedEvents = true;

                foreach (var rectangleVM in rectangleVMsArray)
                    _rectangles.Add(rectangleVM);
            }
            finally
            {
                SuppressSelectionChangedEvents = false;

                RaiseSelectionChanged(Array.Empty<HotspotRectangleVM>(), rectangleVMsArray);
            }
        }

        public bool IsSelected(HotspotRectangleVM rectangleVM)
        {
            return Rectangles.Contains(rectangleVM);
        }


        private void Rectangles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!SuppressSelectionChangedEvents)
            {
                RaiseSelectionChanged(
                    e.OldItems?.OfType<HotspotRectangleVM>().ToArray() ?? Array.Empty<HotspotRectangleVM>(),
                    e.NewItems?.OfType<HotspotRectangleVM>().ToArray() ?? Array.Empty<HotspotRectangleVM>());
            }
        }
    }
}
