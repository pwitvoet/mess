
namespace MESS.Mapping
{
    /// <summary>
    /// Base class for brushes, entities and groups.
    /// </summary>
    public abstract class MapObject
    {
        private Group _group;
        public Group Group
        {
            get => _group;
            set
            {
                _group?.Objects.Remove(this);
                _group = value;
                _group?.Objects.Add(this);
            }
        }

        private VisGroup _visGroup;
        public VisGroup VisGroup
        {
            get => _visGroup;
            set
            {
                _visGroup?.Objects.Remove(this);
                _visGroup = value;
                _visGroup?.Objects.Add(this);
            }
        }

        public Color Color { get; set; }
    }
}
