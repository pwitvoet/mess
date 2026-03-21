namespace MESS.Mathematics.Spatial
{
    public struct Vector3D
    {
        public double X;
        public double Y;
        public double Z;

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object? obj) => obj is Vector3D other && this == other;

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 23 + X.GetHashCode();
            hash = hash * 23 + Y.GetHashCode();
            hash = hash * 23 + Z.GetHashCode();
            return hash;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";


        public double Length() => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public double SquaredLength() => (X * X) + (Y * Y) + (Z * Z);

        public Vector3D Normalized() => this / Length();

        public double DotProduct(Vector3D other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

        public Vector3D CrossProduct(Vector3D other)
        {
            return new Vector3D(
                (Y * other.Z) - (Z * other.Y),
                (Z * other.X) - (X * other.Z),
                (X * other.Y) - (Y * other.X));
        }

        public Vector3D GetPerpendicularVector()
        {
            var absX = Math.Abs(X);
            var absY = Math.Abs(Y);
            var absZ = Math.Abs(Z);
            if (absZ < absX && absZ < absY)
                return new Vector3D(0, 0, 1).CrossProduct(this);
            else if (absY < absX)
                return new Vector3D(0, 1, 0).CrossProduct(this);
            else
                return new Vector3D(1, 0, 0).CrossProduct(this);
        }


        public static Vector3D operator -(Vector3D vector) => new Vector3D(-vector.X, -vector.Y, -vector.Z);

        public static Vector3D operator +(Vector3D left, Vector3D right) => new Vector3D(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        public static Vector3D operator -(Vector3D left, Vector3D right) => new Vector3D(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Vector3D operator *(Vector3D left, Vector3D right) => new Vector3D(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        public static Vector3D operator *(Vector3D vector, double scalar) => new Vector3D(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
        public static Vector3D operator *(double scalar, Vector3D vector) => vector * scalar;
        public static Vector3D operator /(Vector3D vector, double scalar) => new Vector3D(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);
        public static Vector3D operator /(double scalar, Vector3D vector) => new Vector3D(scalar / vector.X, scalar / vector.Y, scalar / vector.Z);

        public static bool operator ==(Vector3D left, Vector3D right) => left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        public static bool operator !=(Vector3D left, Vector3D right) => left.X != right.X || left.Y != right.Y || left.Z != right.Z;
    }
}
