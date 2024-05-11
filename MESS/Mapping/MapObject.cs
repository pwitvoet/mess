namespace MESS.Mapping
{
    /// <summary>
    /// Base class for brushes, entities and groups.
    /// </summary>
    public abstract class MapObject
    {
        private Group? _group;
        public Group? Group => _group;

        public virtual Group? TopLevelGroup => _group?.TopLevelGroup;

        private List<VisGroup> _visGroups = new();
        public IReadOnlyList<VisGroup> VisGroups => _visGroups;

        public Color Color { get; set; }



        public void RemoveFromGroupAndVisGroups()
        {
            Group?.RemoveObject(this);

            while (VisGroups.Any())
                VisGroups[0].RemoveObject(this);
        }

        internal void SetGroup(Group? group) => _group = group;

        internal void AddVisGroup(VisGroup visGroup) => _visGroups.Add(visGroup);

        internal void RemoveVisGroup(VisGroup visGroup) => _visGroups.Remove(visGroup);
    }
}
