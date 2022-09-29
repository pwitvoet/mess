using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Macros
{
    static class PointDistributionExtensions
    {
        /// <summary>
        /// Triangulates the given face.
        /// </summary>
        public static IEnumerable<Triangle> GetTriangleFan(this Face face)
        {
            var vertex = face.Vertices[0];
            for (int i = 2; i < face.Vertices.Count; i++)
                yield return new Triangle(vertex, face.Vertices[i - 1], face.Vertices[i]);
        }

        /// <summary>
        /// Returns the area of the given triangle.
        /// </summary>
        public static float GetSurfaceArea(this Triangle triangle)
        {
            var a = triangle.Vertex2 - triangle.Vertex1;
            var b = triangle.Vertex3 - triangle.Vertex1;
            var normal = a.CrossProduct(b);
            return normal.Length() / 2;
        }

        /// <summary>
        /// Splits the given brush into a number of tetrahedrons.
        /// </summary>
        public static IEnumerable<Tetrahedron> GetTetrahedrons(this Brush brush)
        {
            var vertex = brush.Faces[0].Vertices[0];
            foreach (var face in brush.Faces.Skip(1))
            {
                if (face.Vertices.Contains(vertex))
                    continue;

                foreach (var triangle in GetTriangleFan(face))
                    yield return new Tetrahedron(triangle.Vertex1, triangle.Vertex2, triangle.Vertex3, vertex);
            }
        }

        /// <summary>
        /// Returns the volume of the given tetrahedron.
        /// </summary>
        public static float GetVolume(this Tetrahedron tetrahedron)
        {
            var a = tetrahedron.Vertex2 - tetrahedron.Vertex1;
            var b = tetrahedron.Vertex3 - tetrahedron.Vertex1;
            var c = tetrahedron.Vertex4 - tetrahedron.Vertex1;
            return Math.Abs(a.CrossProduct(b).DotProduct(c)) / 6;
        }


        /// <summary>
        /// Returns a random point on the given triangle.
        /// </summary>
        public static Vector3D GetRandomPoint(this Triangle triangle, Random random)
        {
            var r1 = (float)Math.Sqrt(random.NextDouble());
            var r2 = (float)random.NextDouble();

            return (triangle.Vertex1 * (1 - r1)) + (triangle.Vertex2 * (r1 * (1 - r2))) + (triangle.Vertex3 * (r1 * r2));
        }

        /// <summary>
        /// Returns a random point inside the given tetrahedron.
        /// </summary>
        public static Vector3D GetRandomPoint(this Tetrahedron tetrahedron, Random random)
        {
            var s = (float)random.NextDouble();
            var t = (float)random.NextDouble();
            var u = (float)random.NextDouble();

            if (s + t > 1)
            {
                // Fold the cube into a prism:
                s = 1 - s;
                t = 1 - t;
            }

            if (t + u > 1)
            {
                // Fold the prism into a tetrahedron:
                var temp = u;
                u = 1 - s - t;
                t = 1 - temp;
            }
            else if (s + t + u > 1)
            {
                var temp = u;
                u = s + t + u - 1;
                s = 1 - t - temp;
            }

            var a = 1 - s - t - u;  // (a, s, t, u) are the barycentric coordinates of the random point.
            return (tetrahedron.Vertex1 * a) + (tetrahedron.Vertex2 * s) + (tetrahedron.Vertex3 * t) + (tetrahedron.Vertex4 * u);
        }
    }
}
