using System;

namespace MESS.Mathematics.Spatial
{
    public struct Vector3D
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object obj) => obj is Vector3D other && this == other;

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 23 + X.GetHashCode();
            hash = hash * 23 + Y.GetHashCode();
            hash = hash * 23 + Z.GetHashCode();
            return hash;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";


        public float Length() => (float)Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public float SquaredLength() => (X * X) + (Y * Y) + (Z * Z);

        public Vector3D Normalized() => this / Length();

        public float DotProduct(Vector3D other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

        public Vector3D CrossProduct(Vector3D other)
        {
            return new Vector3D(
                (Y * other.Z) - (Z * other.Y),
                (Z * other.X) - (X * other.Z),
                (X * other.Y) - (Y * other.X));
        }


        public static Vector3D operator +(Vector3D left, Vector3D right) => new Vector3D(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        public static Vector3D operator -(Vector3D left, Vector3D right) => new Vector3D(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Vector3D operator *(Vector3D vector, float scalar) => new Vector3D(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
        public static Vector3D operator *(float scalar, Vector3D vector) => vector * scalar;
        public static Vector3D operator /(Vector3D vector, float scalar) => new Vector3D(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);
        public static Vector3D operator /(float scalar, Vector3D vector) => vector / scalar;

        public static bool operator ==(Vector3D left, Vector3D right) => left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        public static bool operator !=(Vector3D left, Vector3D right) => left.X != right.X || left.Y != right.Y || left.Z != right.Z;
    }
}
