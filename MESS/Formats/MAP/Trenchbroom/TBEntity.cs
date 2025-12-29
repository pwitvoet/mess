using MESS.Mapping;

namespace MESS.Formats.MAP.Trenchbroom
{
    public class TBEntity : Entity
    {
        public string? ProtectedProperties { get; set; }


        public override Entity PartialCopy()
        {
            var copy = new TBEntity();
            PartialCopyTo(copy);

            copy.ProtectedProperties = ProtectedProperties;
            return copy;
        }
    }
}
