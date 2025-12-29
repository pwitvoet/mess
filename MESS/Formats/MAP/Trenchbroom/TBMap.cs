using MESS.Mapping;

namespace MESS.Formats.MAP.Trenchbroom
{
    public class TBMap : Map
    {
        public string? Game { get; set; }
        public string? Format { get; set; }


        public override Map PartialCopy()
        {
            var copy = new TBMap();
            PartialCopyTo(copy);

            copy.Game = Game;
            copy.Format = Format;
            return copy;
        }
    }
}
