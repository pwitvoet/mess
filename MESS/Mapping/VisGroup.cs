namespace MESS.Mapping
{
    /// <summary>
    /// Visibility groups allow a level-designer to quickly show or hide related objects in the editor.
    /// <para>
    /// In .jmf files, objects can belong to multiple visgroups, and they are linked to visgroups individually,
    /// regardless of how they are grouped. In .jmf files, groups are not linked to visgroups.
    /// </para>
    /// <para>
    /// In .rmf and TrenchBroom .map files, objects can only belong to one visgroup (or none).
    /// In these formats, only top-level objects are directly linked to a visgroup.
    /// Grouped objects implicitly belong to the visgroup of their parent.
    /// </para>
    /// </summary>
    public class VisGroup
    {
        public int ID { get; set; }
        public string? Name { get; set; }
        public Color Color { get; set; }
        public bool IsVisible { get; set; }

        private List<MapObject> _objects = new();
        public IReadOnlyList<MapObject> Objects => _objects;


        public void AddObject(MapObject mapObject)
        {
            _objects.Add(mapObject);
            mapObject.AddVisGroup(this);
        }

        public void AddObjects(IEnumerable<MapObject> mapObjects)
        {
            foreach (var mapObject in mapObjects)
                AddObject(mapObject);
        }

        public void RemoveObject(MapObject mapObject)
        {
            _objects.Remove(mapObject);
            mapObject.RemoveVisGroup(this);
        }
    }
}
