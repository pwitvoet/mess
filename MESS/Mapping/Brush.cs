using System.Collections.Generic;

namespace MESS.Mapping
{
    /// <summary>
    /// Brushes are 3-dimensional textured convex shapes, such as cubes or cylinders.
    /// </summary>
    public class Brush : MapObject
    {
        public List<Face> Faces { get; } = new List<Face>();
    }
}
