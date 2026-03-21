namespace MESS.Mathematics.Spatial
{
    public class Polygon3D
    {
        private Vector3D[] _vertices;
        public IReadOnlyList<Vector3D> Vertices => _vertices;


        public Polygon3D(IEnumerable<Vector3D> vertices)
        {
            _vertices = vertices.ToArray();
        }

        /// <summary>
        /// Clips the polygon by the given plane.
        /// Returns a clipped copy of the polygon if the plane intersects the polygon.
        /// Returns the polygon itself if it's entirely behind the clipping plane.
        /// Returns null if the polygon is entirely in front of the clipping plane.
        /// </summary>
        public Polygon3D? Clip(Plane plane)
        {
            var planeDistances = new double[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
                planeDistances[i] = _vertices[i].DotProduct(plane.Normal) - plane.Distance;

            // If all vertices are behind (or on) the clipping plane, then the polygon remains as it is:
            if (planeDistances.All(distance => distance <= 0))
                return this;

            // If all vertices are in front of (or some on) the clipping plane, the entire polygon will be clipped away:
            if (planeDistances.All(distance => distance >= 0))
                return null;


            // Keep vertices that are behind or on the clipping plane, remove those that are in front,
            // and add intersection points for edges that are being intersected by the clipping plane:
            var newVertices = new List<Vector3D>();
            var prevI = planeDistances.Length - 1;
            for (int i = 0; i < _vertices.Length; i++)
            {
                var curDistance = planeDistances[i];
                var prevDistance = planeDistances[prevI];

                if ((prevDistance < 0 && curDistance > 0) || (prevDistance > 0 && curDistance < 0))
                {
                    var totalDistance = Math.Abs(prevDistance) + Math.Abs(curDistance);
                    var scalar = Math.Abs(prevDistance) / totalDistance;
                    var intersectionPoint = _vertices[prevI] + (_vertices[i] - _vertices[prevI]) * scalar;
                    newVertices.Add(intersectionPoint);
                }

                if (curDistance <= 0)
                    newVertices.Add(_vertices[i]);

                prevI = i;
            }
            return new Polygon3D(newVertices);
        }
    }
}
