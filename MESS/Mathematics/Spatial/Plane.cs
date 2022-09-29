namespace MESS.Mathematics.Spatial
{
    public struct Plane
    {
        /// <summary>
        /// Returns the plane that contains the given 3 points. The points should be in clockwise order.
        /// </summary>
        public static Plane FromPoints(Vector3D[] points)
        {
            var edge1 = points[1] - points[0];
            var edge2 = points[2] - points[0];

            var normal = edge2.CrossProduct(edge1).Normalized();
            return new Plane(normal, normal.DotProduct(points[0]));
        }


        public Vector3D Normal;
        public float Distance;

        public Plane(Vector3D normal, float distance)
        {
            Normal = normal;
            Distance = distance;
        }

        public Line? Intersection(Plane other)
        {
            var direction = Normal.CrossProduct(other.Normal);
            var determinant = direction.SquaredLength();
            if (determinant == 0) // TODO: Use some kind of epsilon
                return null;    // Planes are parallel.

            var point = ((direction.CrossProduct(other.Normal) * Distance) + (Normal.CrossProduct(direction) * other.Distance)) / determinant;
            return new Line(point, direction);
        }

        public static Vector3D? IntersectionPoint(Plane plane1, Plane plane2, Plane plane3)
        {
            var m1 = new Vector3D(plane1.Normal.X, plane2.Normal.X, plane3.Normal.X);
            var m2 = new Vector3D(plane1.Normal.Y, plane2.Normal.Y, plane3.Normal.Y);
            var m3 = new Vector3D(plane1.Normal.Z, plane2.Normal.Z, plane3.Normal.Z);

            var u = m2.CrossProduct(m3);
            var denom = m1.DotProduct(u);
            if (Math.Abs(denom) < float.Epsilon)
                return null;    // No intersection *point*

            var d = new Vector3D(plane1.Distance, plane2.Distance, plane3.Distance);
            var v = m1.CrossProduct(d);
            var ood = 1f / denom;
            return new Vector3D(
                d.DotProduct(u) * ood,
                m3.DotProduct(v) * ood,
                -m2.DotProduct(v) * ood);
        }
    }
}
