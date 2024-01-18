using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Formats.JMF
{
    public class JmfFace : Face
    {
        public int RenderFlags { get; set; }
        public JmfTextureAlignment TextureAlignment { get; set; }
        public byte[]? UnknownData { get; set; }
        public JmfSurfaceContents Contents { get; set; }
        public JmfAxisAlignment AxisAlignment { get; set; }

        public List<Vector2D> VertexUVCoordinates { get; } = new();
        public List<JmfVertexSelection> VertexSelectionState { get; } = new();
    }

    [Flags]
    public enum JmfTextureAlignment
    {
        None =  0x00,
        World = 0x01,
        Face =  0x02,
    }

    [Flags]
    public enum JmfSurfaceContents
    {
        None =   0x00,
        Detail = 0x08,
    }

    public enum JmfAxisAlignment
    {
        XAligned =  0,
        YAligned =  1,
        ZAligned =  2,
        Unaligned = 3,
    }

    public enum JmfVertexSelection
    {
        None = 0,
        IsSelected = 1,
        IsMidpointSelected = 2,
    }
}
