using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    /// <summary>
    /// Brushes are 3-dimensional textured convex shapes, such as cubes or cylinders.
    /// </summary>
    public class Brush : MapObject
    {
        public IReadOnlyList<Face> Faces { get; }
        public BoundingBox BoundingBox { get; }


        // Editor state:
        public bool IsSelected { get; set; }
        public bool IsHidden { get; set; }


        public Brush(IEnumerable<Face> faces)
        {
            Faces = faces.ToArray();

            InitializePlanes();
            InitializeVertices();
            BoundingBox = BoundingBox.FromPoints(Faces.SelectMany(face => face.Vertices));
        }


        private void InitializePlanes()
        {
            foreach (var face in Faces)
            {
                if (face.Plane.Normal == default)
                    face.Plane = Plane.FromPoints(face.PlanePoints);
            }
        }

        private void InitializeVertices()
        {
            if (Faces.All(face => face.Vertices.Count > 0))
                return;

            foreach (var face in Faces)
                face.Vertices.Clear();

            // TODO: Replace this with a more efficient clipping algorithm:
            for (int i = 0; i < Faces.Count; i++)
            {
                for (int j = i + 1; j < Faces.Count; j++)
                {
                    for (int k = j + 1; k < Faces.Count; k++)
                    {
                        var face1 = Faces[i];
                        var face2 = Faces[j];
                        var face3 = Faces[k];
                        if (Plane.IntersectionPoint(face1.Plane, face2.Plane, face3.Plane) is Vector3D point &&
                            this.Contains(point, epsilon: 1f))  // TODO: Figure out what a reasonable epsilon is, and whether this can cause any problems... (a 0 epsilon will sometimes omit vertices!)
                        {
                            if (!face1.Vertices.Contains(point)) face1.Vertices.Add(point);
                            if (!face2.Vertices.Contains(point)) face2.Vertices.Add(point);
                            if (!face3.Vertices.Contains(point)) face3.Vertices.Add(point);
                        }
                    }
                }
            }

            // Store vertices in clockwise order:
            foreach (var face in Faces)
            {
                var center = new Vector3D();
                foreach (var vertex in face.Vertices)
                    center += vertex;
                center /= face.Vertices.Count;

                var normal = face.Plane.Normal;
                var forward = (face.Vertices[0] - center).Normalized();
                var right = forward.CrossProduct(normal).Normalized();

                var clockwiseVertices = face.Vertices
                    .Select(vertex =>
                    {
                        var point = vertex - center;
                        var x = point.DotProduct(right);
                        var y = point.DotProduct(forward);
                        return new { Angle = Math.Atan2(y, x), Vertex = vertex };
                    })
                    .OrderByDescending(item => item.Angle)
                    .Select(item => item.Vertex)
                    .ToArray();
                face.Vertices.Clear();
                face.Vertices.AddRange(clockwiseVertices);
            }
        }
    }
}
