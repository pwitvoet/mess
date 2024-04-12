namespace MESS.Formats
{
    public enum ValueTooLongHandling
    {
        /// <summary>
        /// Truncate values that are too long for the target format, and log a warning.
        /// </summary>
        Truncate,

        /// <summary>
        /// Fail when a value is too long for the target format, and log an error.
        /// </summary>
        Fail,
    }

    public enum InvalidCharacterHandling
    {
        /// <summary>
        /// Replace the invalid character, and log a warning.
        /// </summary>
        Replace,

        /// <summary>
        /// Leave the invalid character, and log a warning.
        /// </summary>
        Ignore,

        /// <summary>
        /// Fail when a key, value or texture name contains an invalid character, and log an error.
        /// </summary>
        Fail,
    }

    public enum TooManyVisGroupsHandling
    {
        /// <summary>
        /// Put a brush or entity that belongs to multiple VIS groups in the first group only, and log a warning.
        /// </summary>
        UseFirst,

        /// <summary>
        /// Put a brush or entity that belongs to multiple VIS groups in the last group only, and log a warning.
        /// </summary>
        UseLast,

        /// <summary>
        /// Fail when a brush or entity belongs to multiple VIS groups, and log an error.
        /// </summary>
        Fail,
    }

    public abstract class FileSaveSettings
    {
        /// <summary>
        /// What to do when a property key or value is too long for the target format.
        /// RMF files have a limit of 255 ASCII characters.
        /// </summary>
        public ValueTooLongHandling KeyValueTooLongHandling { get; set; } = ValueTooLongHandling.Fail;

        /// <summary>
        /// What to do when a texture name is too long for the target format.
        /// RMF files have a limit of 260 ASCII characters (40 for file version 1.6 and older).
        /// JMF files have a limit of 64 ASCII characters.
        /// </summary>
        public ValueTooLongHandling TextureNameTooLongHandling { get; set; } = ValueTooLongHandling.Fail;

        /// <summary>
        /// What to do when a property key or value contains invalid characters (double quotes).
        /// </summary>
        public InvalidCharacterHandling KeyValueInvalidCharacterHandling { get; set; } = InvalidCharacterHandling.Replace;
        public string KeyValueInvalidCharacterReplacement { get; set; } = "";

        /// <summary>
        /// What to do when a texture name contains invalid characters (spaces).
        /// </summary>
        public InvalidCharacterHandling TextureNameInvalidCharacterHandling { get; set; } = InvalidCharacterHandling.Replace;
        public string TextureNameInvalidCharacterReplacement { get; set; } = "";

        /// <summary>
        /// What to do when a brush or entity belongs to multiple VIS groups, but the target format only supports one VIS group per object.
        /// If the target format does not support VIS groups at all, then VIS group data is silently discarded.
        /// </summary>
        public TooManyVisGroupsHandling TooManyVisGroupsHandling { get; set; } = TooManyVisGroupsHandling.UseFirst;


        public FileSaveSettings(FileSaveSettings? settings = null)
        {
            if (settings != null)
            {
                KeyValueTooLongHandling = settings.KeyValueTooLongHandling;
                TextureNameTooLongHandling = settings.TextureNameTooLongHandling;
                KeyValueInvalidCharacterHandling = settings.KeyValueInvalidCharacterHandling;
                KeyValueInvalidCharacterReplacement = settings.KeyValueInvalidCharacterReplacement;
                TextureNameInvalidCharacterHandling = settings.TextureNameInvalidCharacterHandling;
                TextureNameInvalidCharacterReplacement = settings.TextureNameInvalidCharacterReplacement;
                TooManyVisGroupsHandling = settings.TooManyVisGroupsHandling;
            }
        }
    }
}
