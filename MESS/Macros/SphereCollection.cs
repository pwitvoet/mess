using MESS.Mathematics.Spatial;

namespace MESS.Macros
{
    /// <summary>
    /// Used to prevent template instances from overlapping each other.
    /// </summary>
    class SphereCollection
    {
        // TODO: Replace this with a more optimized data-structure, such as a grid or quadtree, for performance improvements!
        private List<Sphere> _spheres = new();


        /// <summary>
        /// Returns true if the given spherical area is available, false if the space is already (partially) occupied by other spheres.
        /// </summary>
        public bool TryInsert(Vector3D position, float radius)
        {
            if (_spheres.Any(HasOverlap))
                return false;

            _spheres.Add(new Sphere(position, radius));
            return true;

            bool HasOverlap(Sphere sphere)
            {
                var offset = sphere.Position - position;
                var combinedRadius = sphere.Radius + radius;
                return offset.SquaredLength() < combinedRadius * combinedRadius;
            }
        }


        struct Sphere
        {
            public Vector3D Position { get; }
            public float Radius { get; }

            public Sphere(Vector3D position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }
    }
}
