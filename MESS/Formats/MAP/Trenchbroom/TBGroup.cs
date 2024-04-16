using MESS.Mapping;

namespace MESS.Formats.MAP.Trenchbroom
{
    public class TBGroup : Group
    {
        public string Name { get; set; } = "";

        public string? LinkedGroupID { get; set; }
        public string? LinkedGroupTransformation { get; set; }
    }
}
