namespace MESS.Mathematics.Spatial
{
    public struct Line
    {
        public Vector3D Point;
        public Vector3D Direction;

        public Line(Vector3D point, Vector3D direction)
        {
            Point = point;
            Direction = direction;
        }

        public override string ToString() => $"{Point} +{Direction}";
    }
}
