namespace MESS.Formats.RMF
{
    public enum RmfSpawnflagsPropertyHandling
    {
        /// <summary>
        /// If an entity contains a spawnflags property, ignore it, and log a warning if its value doesn't match the spawnflags field.
        /// Use this for rmf files that have only been modified with Hammer.
        /// </summary>
        Ignore,

        /// <summary>
        /// If an entity contains a spawnflags property, use its value instead of the spawnflags field, and log a warning if its value doesn't match the spawnflags field.
        /// Use this for rmf files that have been modified with JACK.
        /// </summary>
        Use,

        /// <summary>
        /// Fail if an entity contains a spawnflags property, and its value doesn't match the spawnflags field. Also log an error.
        /// </summary>
        Fail,
    }

    public class RmfFileLoadSettings : FileLoadSettings
    {
        /// <summary>
        /// What to do when an entity contains a spawnflags property (which is duplicate data, because entities already have a spawnflags field).
        /// </summary>
        public RmfSpawnflagsPropertyHandling SpawnflagsPropertyHandling { get; set; }


        public RmfFileLoadSettings(FileLoadSettings? settings = null)
            : base(settings)
        {
        }
    }
}
