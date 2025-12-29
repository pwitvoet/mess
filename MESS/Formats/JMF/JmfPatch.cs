using MESS.Mathematics.Spatial;

namespace MESS.Formats.JMF
{
    /// <summary>
    /// A patch made from quadratic Bezier curves.
    /// </summary>
    public class JmfPatch
    {
        public int Columns { get; }
        public int Rows { get; }
        public JmfPatchControlPoint[,] ControlPoints { get; }

        public string TextureName { get; set; } = "";
        public Vector3D TextureRightAxis { get; set; }
        public Vector3D TextureDownAxis { get; set; }
        public Vector2D TextureShift { get; set; }
        public float TextureAngle { get; set; }
        public Vector2D TextureScale { get; set; }

        public JmfTextureAlignment TextureAlignment { get; set; }
        public byte[]? UnknownData { get; set; }
        public JmfSurfaceContents Contents { get; set; }
        public byte[]? UnknownData2 { get; set; }


        public JmfPatch(int columns, int rows)
        {
            Columns = columns;
            Rows = rows;

            ControlPoints = new JmfPatchControlPoint[columns, rows];
        }

        public JmfPatch Copy()
        {
            var copy = new JmfPatch(Columns, Rows);

            for (int column = 0; column < Columns; column++)
                for (int row = 0; row < Rows; row++)
                    copy.ControlPoints[column, row] = ControlPoints[column, row].Copy();

            copy.TextureName = TextureName;
            copy.TextureRightAxis = TextureRightAxis;
            copy.TextureDownAxis = TextureDownAxis;
            copy.TextureShift = TextureShift;
            copy.TextureAngle = TextureAngle;
            copy.TextureScale = TextureScale;

            copy.TextureAlignment = TextureAlignment;
            copy.UnknownData = UnknownData?.ToArray();
            copy.Contents = Contents;
            copy.UnknownData2 = UnknownData2?.ToArray();

            return copy;
        }
    }

    public class JmfPatchControlPoint
    {
        public Vector3D Position { get; set; }
        public Vector3D Normal { get; set; }
        public Vector2D UV { get; set; }


        // Editor state:
        public bool IsSelected { get; set; }


        public JmfPatchControlPoint Copy()
        {
            var copy = new JmfPatchControlPoint();
            copy.Position = Position;
            copy.Normal = Normal;
            copy.UV = UV;
            copy.IsSelected = IsSelected;
            return copy;
        }
    }
}
