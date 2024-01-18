namespace MESS.Mapping
{
    /// <summary>
    /// Visibility groups allow a level-designer to quickly show or hide related objects in the editor.
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

        public void RemoveObject(MapObject mapObject)
        {
            _objects.Remove(mapObject);
            mapObject.RemoveVisGroup(this);
        }
    }
}
