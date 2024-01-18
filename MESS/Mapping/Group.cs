namespace MESS.Mapping
{
    /// <summary>
    /// Groups allow a level-designer to quickly select multiple related objects in the editor.
    /// </summary>
    public class Group : MapObject
    {
        public int ID { get; set; }

        private List<MapObject> _objects = new();
        public IReadOnlyList<MapObject> Objects => _objects;

        public bool IsSelected { get; set; }
        public bool IsHidden { get; set; }


        public void AddObject(MapObject mapObject)
        {
            if (mapObject.Group != null)
                mapObject.Group.RemoveObject(mapObject);

            _objects.Add(mapObject);
            mapObject.SetGroup(this);
        }

        public void RemoveObject(MapObject mapObject)
        {
            _objects.Remove(mapObject);
            mapObject.SetGroup(null);
        }
    }
}
