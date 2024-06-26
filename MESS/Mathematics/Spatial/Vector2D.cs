﻿namespace MESS.Mathematics.Spatial
{
    public struct Vector2D
    {
        public float X;
        public float Y;

        public Vector2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override bool Equals(object? obj) => obj is Vector2D other && this == other;

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 23 + X.GetHashCode();
            hash = hash * 23 + Y.GetHashCode();
            return hash;
        }

        public override string ToString() => $"({X}, {Y})";


        public float Length() => (float)Math.Sqrt((X * X) + (Y * Y));

        public float SquaredLength() => (X * X) + (Y * Y);

        public Vector2D Normalized() => this / Length();

        public float DotProduct(Vector2D other) => (X * other.X) + (Y * other.Y);


        public static Vector2D operator -(Vector2D vector) => new Vector2D(-vector.X, -vector.Y);

        public static Vector2D operator +(Vector2D left, Vector2D right) => new Vector2D(left.X + right.X, left.Y + right.Y);
        public static Vector2D operator -(Vector2D left, Vector2D right) => new Vector2D(left.X - right.X, left.Y - right.Y);

        public static Vector2D operator *(Vector2D vector, float scalar) => new Vector2D(vector.X * scalar, vector.Y * scalar);
        public static Vector2D operator *(float scalar, Vector2D vector) => vector * scalar;
        public static Vector2D operator /(Vector2D vector, float scalar) => new Vector2D(vector.X / scalar, vector.Y / scalar);
        public static Vector2D operator /(float scalar, Vector2D vector) => vector / scalar;

        public static bool operator ==(Vector2D left, Vector2D right) => left.X == right.X && left.Y == right.Y;
        public static bool operator !=(Vector2D left, Vector2D right) => left.X != right.X || left.Y != right.Y;
    }
}
