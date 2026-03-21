using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    /// <summary>
    /// Brushes are 3-dimensional textured convex shapes, such as cubes or cylinders.
    /// </summary>
    public class Brush : MapObject
    {
        public IReadOnlyList<Face> Faces { get; private set; }
        public BoundingBox BoundingBox { get; private set; }


        // Editor state:
        public bool IsSelected { get; set; }
        public bool IsHidden { get; set; }


        public Brush(IEnumerable<Face> faces)
        {
            Faces = InitializeFaces(faces.ToArray());
            BoundingBox = BoundingBox.FromPoints(Faces.SelectMany(face => face.Vertices));
        }

        public bool IsValid()
            => Faces.Count >= 4 && Faces.All(face => face.Vertices.Count >= 3);

        public void RemoveInvalidFaces()
        {
            Faces = Faces.Where(face => face.Vertices.Count >= 3).ToArray();
        }

        /// <summary>
        /// Creates a copy of this brush that includes editor format-specific data.
        /// Faces will be copied, but group and VIS group information is excluded.
        /// </summary>
        public virtual Brush PartialCopy()
        {
            var copy = new Brush(Array.Empty<Face>());
            PartialCopyTo(copy);
            return copy;
        }

        protected void PartialCopyTo(Brush other)
        {
            other.Color = Color;
            other.Faces = Faces.Select(face => face.Copy()).ToArray();
            other.BoundingBox = BoundingBox;
            other.IsSelected = IsSelected;
            other.IsHidden = IsHidden;
        }



        private static Face[] InitializeFaces(IReadOnlyList<Face> faces)
        {
            // Initialize planes:
            foreach (var face in faces)
            {
                if (face.Plane.Normal == default)
                    face.Plane = Plane.FromPoints(face.PlanePoints);
            }

            // Create a polygon for each face:
            var polygons = new Polygon3D?[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                var normal = faces[i].Plane.Normal;
                var axis1 = normal.GetPerpendicularVector().Normalized();
                var axis2 = normal.CrossProduct(axis1);

                // TODO: Make the radius configurable somewhere?
                var radius = 100_000.0;
                var center = normal * faces[i].Plane.Distance;
                Polygon3D? polygon = new Polygon3D(new Vector3D[] {
                    center + axis1 * radius + axis2 * radius,
                    center + axis1 * radius - axis2 * radius,
                    center - axis1 * radius - axis2 * radius,
                    center - axis1 * radius + axis2 * radius,
                });

                for (int j = 0; j < faces.Count; j++)
                {
                    if (i == j)
                        continue;

                    polygon = polygon.Clip(faces[j].Plane);
                    if (polygon is null)
                        break;
                }

                polygons[i] = polygon;
            }

            // Add polygon vertices to their respective face, and filter out empty faces/polygons:
            return polygons
                .Where(polygon => polygon != null)
                .Select((polygon, i) =>
                {
                    var face = faces[i];
                    face.Vertices.Clear();
                    face.Vertices.AddRange(polygon!.Vertices);
                    MergeNearbyVertices(face, 1.0 / 64);
                    return face;
                })
                .ToArray();
        }

        private static void MergeNearbyVertices(Face face, double threshold)
        {
            var squaredThreshold = threshold * threshold;
            for (int i = 0; i < face.Vertices.Count; i++)
            {
                var vertex = face.Vertices[i];
                for (int j = i + 1; j < face.Vertices.Count; j++)
                {
                    var otherVertex = face.Vertices[j];
                    if ((vertex - otherVertex).SquaredLength() < squaredThreshold)
                    {
                        face.Vertices.RemoveAt(j);
                        j -= 1;
                    }
                }
            }
        }
    }
}
