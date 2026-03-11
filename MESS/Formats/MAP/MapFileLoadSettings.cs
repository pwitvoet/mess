namespace MESS.Formats.MAP
{
    public enum InvalidBrushHandling
    {
        /// <summary>
        /// The map parser will fail when it encounters an invalid brush.
        /// </summary>
        Fail,

        /// <summary>
        /// The map parser will try to salvage invalid brushes by discarding invalid faces.
        /// Brush that are still invalid will be discarded.
        /// </summary>
        DiscardFaces,

        /// <summary>
        /// The map parser will discard invalid brushes.
        /// </summary>
        DiscardBrush,

        /// <summary>
        /// The map parser will keep invalid brushes and faces. This minimizes data loss, but may cause problems later on when processing the map.
        /// </summary>
        KeepBrush,
    }

    public enum TrenchbroomGroupHandling
    {
        /// <summary>
        /// Convert Trenchbroom groups (which are stored as func_group entities with special properties) and layers to groups and VIS groups,
        /// and remove Trenchbroom-specific properties after using them to determine which group and VIS group an object belongs to.
        /// </summary>
        ConvertToGroup,

        /// <summary>
        /// Leave Trenchbroom groups as func_group entities and do not remove any Trenchbroom-specific properties.
        /// </summary>
        LeaveAsEntity,
    }

    public class MapFileLoadSettings : FileLoadSettings
    {
        /// <summary>
        /// What to do when an invalid brush is encountered.
        /// </summary>
        public InvalidBrushHandling InvalidBrushHandling { get; set; }

        /// <summary>
        /// How to handle Trenchbroom groups and layers, which are stored as func_group entities with Trenchbroom-specific properties.
        /// </summary>
        public TrenchbroomGroupHandling TrenchbroomGroupHandling { get; set; }


        public MapFileLoadSettings(FileLoadSettings? settings = null)
            : base(settings)
        {
        }
    }
}
