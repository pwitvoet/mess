using MESS.Mapping;

namespace MESS.Formats.JMF
{
    public class JmfBrush : Brush
    {
        public JmfMapObjectFlags JmfFlags { get; set; }
        public JmfPatch? Patch { get; set; }


        public JmfBrush(IEnumerable<Face> faces)
            : base(faces)
        {
        }
    }
}
