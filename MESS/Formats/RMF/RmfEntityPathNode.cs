using MESS.Mapping;

namespace MESS.Formats.RMF
{
    public class RmfEntityPathNode : EntityPathNode
    {
        // NOTE: Initially this was called 'Index', but path nodes are stored in order of appearance.
        //       This field actually indicates in which order they were created:
        public int CreationOrder { get; set; }
    }
}
