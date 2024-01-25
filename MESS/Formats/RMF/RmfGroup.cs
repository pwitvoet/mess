using MESS.Mapping;

namespace MESS.Formats.RMF
{
    public class RmfGroup : Group, IRmfIndexedObject
    {
        public int RmfIndex { get; set; }
    }
}
