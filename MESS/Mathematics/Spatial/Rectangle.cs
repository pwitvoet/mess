namespace MESS.Mathematics.Spatial
{
    public struct Rectangle
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public Vector2D TopLeft => new Vector2D(X, Y);
        public Vector2D BottomRight => new Vector2D(X + Width, Y + Height);

        public double Surface => Width * Height;


        public Rectangle(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
