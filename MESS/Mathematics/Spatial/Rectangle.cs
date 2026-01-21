namespace MESS.Mathematics.Spatial
{
    public struct Rectangle
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public Vector2D TopLeft => new Vector2D(X, Y);
        public Vector2D BottomRight => new Vector2D(X + Width, Y + Height);

        public float Surface => Width * Height;


        public Rectangle(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
