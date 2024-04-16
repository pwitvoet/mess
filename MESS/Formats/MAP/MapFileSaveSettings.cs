namespace MESS.Formats.MAP
{
    public enum MapFileVariant
    {
        /// <summary>
        /// The Valve 220 map format is an evolution of the Quake map format.
        /// It adds texture UV axis, for finer control over texture projection.
        /// </summary>
        Valve220,

        /// <summary>
        /// Similar to the Valve 220 map format, Trenchbroom map files add groups, layers (VIS groups)
        /// and other editor information using func_group entities and special _tb* properties.
        /// </summary>
        TrenchbroomValve220,
    }

    public class MapFileSaveSettings : FileSaveSettings
    {
        /// <summary>
        /// The maximum number of decimals to use for numeric values (plane points and texture axis, scale and angle).
        /// Uses roundtrip format when null.
        /// </summary>
        public int? DecimalsCount { get; set; }

        /// <summary>
        /// The target map file variant.
        /// </summary>
        public MapFileVariant FileVariant { get; set; } = MapFileVariant.Valve220;

        /// <summary>
        /// The game specifier for Trenchbroom map files. This is put in a comment at the top of the map file: // Game: GAME_NAME
        /// </summary>
        public string? TrenchbroomGameName { get; set; }


        public MapFileSaveSettings(FileSaveSettings? settings = null)
            : base(settings)
        {
        }
    }
}
