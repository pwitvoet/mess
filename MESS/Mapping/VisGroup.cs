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

        public List<MapObject> Objects { get; } = new();
    }
}
