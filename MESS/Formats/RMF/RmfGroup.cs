using MESS.Mapping;

namespace MESS.Formats.RMF
{
    public class RmfGroup : Group, IRmfIndexedObject
    {
        public int RmfIndex { get; set; }


        public override Group PartialCopy()
        {
            var copy = new RmfGroup();
            PartialCopyTo(copy);

            copy.RmfIndex = RmfIndex;
            return copy;
        }
    }
}
