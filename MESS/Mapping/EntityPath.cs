namespace MESS.Mapping
{
    public enum PathType
    {
        OneWay = 0,
        Circular = 1,
        PingPong = 2,
    }

    /// <summary>
    /// Entity paths can be used to create a trail of connected entities of a certain kind, typically 'path_corner' or 'path_track'.
    /// </summary>
    public class EntityPath
    {
        public string Name { get; set; } = "";
        public string ClassName { get; set; } = "";
        public PathType Type { get; set; }
        public List<EntityPathNode> Nodes { get; } = new();
        public Color Color { get; set; } = new Color(255, 255, 255);
    }
}
