using MESS.Mapping;

namespace MESS.Formats.MAP.Trenchbroom
{
    public class TBLayer : VisGroup
    {
        public bool IsLocked { get; set; }
        public bool IsOmittedFromExport { get; set; }


        public override VisGroup PartialCopy()
        {
            var copy = new TBLayer();
            PartialCopyTo(copy);

            copy.IsLocked = IsLocked;
            copy.IsOmittedFromExport = IsOmittedFromExport;
            return copy;
        }
    }
}
