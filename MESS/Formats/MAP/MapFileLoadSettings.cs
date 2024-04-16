namespace MESS.Formats.MAP
{
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
        /// How to handle Trenchbroom groups and layers, which are stored as func_group entities with Trenchbroom-specific properties.
        /// </summary>
        public TrenchbroomGroupHandling TrenchbroomGroupHandling { get; set; }


        public MapFileLoadSettings(FileLoadSettings? settings = null)
            : base(settings)
        {
        }
    }
}
