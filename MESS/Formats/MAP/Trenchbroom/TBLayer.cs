using MESS.Mapping;

namespace MESS.Formats.MAP.Trenchbroom
{
    public class TBLayer : VisGroup
    {
        public bool IsLocked { get; set; }
        public bool IsOmittedFromExport { get; set; }
    }
}
