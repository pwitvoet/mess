using MESS.Mapping;

namespace MESS.Formats.RMF
{
    public class RmfBrush : Brush, IRmfIndexedObject
    {
        public int RmfIndex { get; set; }


        public RmfBrush(IEnumerable<Face> faces)
            : base(faces)
        {
        }
    }
}
