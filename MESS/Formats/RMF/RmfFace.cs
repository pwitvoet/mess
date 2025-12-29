using MESS.Mapping;

namespace MESS.Formats.RMF
{
    public class RmfFace : Face
    {
        public byte[]? UnknownData1 { get; set; }


        public override Face Copy()
        {
            var copy = new RmfFace();
            CopyTo(copy);

            copy.UnknownData1 = UnknownData1?.ToArray();
            return copy;
        }
    }
}
