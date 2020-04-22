using System.Collections.Generic;

namespace MESS.Mapping
{
    public enum PathType
    {
        OneWay = 0,
        Circular = 1,
        PingPong = 2,
    }

    /// <summary>
    /// Paths can be used to create a trail of connected entities of a certain kind, typically 'path_corner' or 'path_track'.
    /// </summary>
    public class Path
    {
        public string Name { get; set; }
        public string ClassName { get; set; }
        public PathType Type { get; set; }
        public List<Corner> Corners { get; } = new List<Corner>();
    }
}
