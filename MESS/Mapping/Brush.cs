using MESS.Mathematics.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Mapping
{
    /// <summary>
    /// Brushes are 3-dimensional textured convex shapes, such as cubes or cylinders.
    /// </summary>
    public class Brush : MapObject
    {
        public IReadOnlyList<Face> Faces { get; }
        public BoundingBox BoundingBox { get; }


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
                face.Plane = Plane.FromPoints(face.PlanePoints);
        }

        // TODO: ORDER the resulting vertices (clockwise)!
        private void InitializeVertices()
        {
            if (Faces.All(face => face.Vertices.Count > 0))
                return;

            foreach (var face in Faces)
                face.Vertices.Clear();

            // TODO: This is probably O(n^3) or so!
            //       Would it be more efficient to, for each face, use all other faces to cut it down to a polygon?
            //       That would be O(n^2) I think??
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
                            // TODO: Look into ORDERING these vertices (CCW or CW) -- it matters for some use-cases!
                            if (!face1.Vertices.Contains(point)) face1.Vertices.Add(point);
                            if (!face2.Vertices.Contains(point)) face2.Vertices.Add(point);
                            if (!face3.Vertices.Contains(point)) face3.Vertices.Add(point);
                        }
                    }
                }
            }
        }
    }
}
