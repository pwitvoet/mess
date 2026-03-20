namespace MESS.Mathematics.Spatial
{
    public struct Vector2D
    {
        public double X;
        public double Y;

        public Vector2D(double x, double y)
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


        public double Length() => Math.Sqrt((X * X) + (Y * Y));

        public double SquaredLength() => (X * X) + (Y * Y);

        public Vector2D Normalized() => this / Length();

        public double DotProduct(Vector2D other) => (X * other.X) + (Y * other.Y);


        public static Vector2D operator -(Vector2D vector) => new Vector2D(-vector.X, -vector.Y);

        public static Vector2D operator +(Vector2D left, Vector2D right) => new Vector2D(left.X + right.X, left.Y + right.Y);
        public static Vector2D operator -(Vector2D left, Vector2D right) => new Vector2D(left.X - right.X, left.Y - right.Y);

        public static Vector2D operator *(Vector2D vector, double scalar) => new Vector2D(vector.X * scalar, vector.Y * scalar);
        public static Vector2D operator *(double scalar, Vector2D vector) => vector * scalar;
        public static Vector2D operator /(Vector2D vector, double scalar) => new Vector2D(vector.X / scalar, vector.Y / scalar);
        public static Vector2D operator /(double scalar, Vector2D vector) => vector / scalar;

        public static bool operator ==(Vector2D left, Vector2D right) => left.X == right.X && left.Y == right.Y;
        public static bool operator !=(Vector2D left, Vector2D right) => left.X != right.X || left.Y != right.Y;
    }
}
