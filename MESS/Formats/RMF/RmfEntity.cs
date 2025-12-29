using MESS.Mapping;

namespace MESS.Formats.RMF
{
    public class RmfEntity : Entity, IRmfIndexedObject
    {
        public int RmfIndex { get; set; }

        public byte[]? UnknownData1 { get; set; }
        public RmfEntityType EntityType { get; set; }
        public int? UnknownData2 { get; set; }


        public RmfEntity(IEnumerable<Brush>? brushes = null)
            : base(brushes)
        {
        }

        public override Entity PartialCopy()
        {
            var copy = new RmfEntity();
            PartialCopyTo(copy);

            copy.RmfIndex = RmfIndex;
            copy.UnknownData1 = UnknownData1?.ToArray();
            copy.EntityType = EntityType;
            copy.UnknownData2 = UnknownData2;
            return copy;
        }
    }

    public enum RmfEntityType
    {
        BrushBased = 0,
        PointBased = 2,
    }
}
