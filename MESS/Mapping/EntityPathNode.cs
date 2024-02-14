using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    /// <summary>
    /// Nodes are part of a path, and are used to generate a trail of connected entities.
    /// </summary>
    public class EntityPathNode
    {
        public Vector3D Position { get; set; }
        public string? NameOverride { get; set; }
        public Dictionary<string, string> Properties { get; } = new();
        public Color Color { get; set; } = new Color(255, 255, 255);


        // Common editor state:
        public bool IsSelected { get; set; }
    }
}
