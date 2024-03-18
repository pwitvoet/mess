using MESS.Logging;
using System.Text;

namespace MESS.Formats
{
    internal class Validation
    {
        /// <summary>
        /// Validates the given texture name. Returns either a valid texture name, or throws a <see cref="MapSaveException"/>, depending on the given file save settings.
        /// Uses ASCII encoding by default.
        /// </summary>
        public static string ValidateTextureName(string textureName, int maxLength, FileSaveSettings settings, ILogger logger, Encoding? encoding = null)
        {
            encoding = encoding ?? Encoding.ASCII;
            var rawTextureName = encoding.GetBytes(textureName);

            if (rawTextureName.Length > maxLength)
            {
                if (settings.TextureNameTooLongHandling == ValueTooLongHandling.Truncate)
                {
                    logger.Warning($"Texture name '{textureName}' is too long and will be truncated.");
                    textureName = encoding.GetString(rawTextureName, 0, maxLength);
                }
                else if (settings.TextureNameTooLongHandling == ValueTooLongHandling.Fail)
                {
                    var errorMessage = $"Texture name '{textureName}' is too long.";
                    logger.Error(errorMessage);
                    throw new MapSaveException(errorMessage);
                }
            }

            if (textureName.Contains(' '))  // TODO: What about tabs and control characters?
            {
                if (settings.TextureNameInvalidCharacterHandling == InvalidCharacterHandling.Replace)
                {
                    var newTextureName = textureName.Replace(" ", settings.TextureNameInvalidCharacterReplacement);
                    logger.Warning($"Texture name '{textureName}' contains invalid characters, replacing with '{newTextureName}'.");
                    textureName = newTextureName;
                }
                else if (settings.TextureNameInvalidCharacterHandling == InvalidCharacterHandling.Ignore)
                {
                    logger.Warning($"Texture name '{textureName}' contains invalid characters, ignoring.");
                }
                else if (settings.TextureNameInvalidCharacterHandling == InvalidCharacterHandling.Fail)
                {
                    var errorMessage = $"Texture name '{textureName}' contains invalid characters.";
                    logger.Error(errorMessage);
                    throw new MapSaveException(errorMessage);
                }
            }

            return textureName;
        }

        /// <summary>
        /// Validates the given key. Returns either a valid key, or throws a <see cref="MapSaveException"/>, depending on the given file save settings.
        /// Uses ASCII encoding by default.
        /// </summary>
        public static string? ValidateKey(string? key, int? maxLength, FileSaveSettings settings, ILogger logger, string logPrefix, Encoding? encoding = null, bool mustBeNullTerminated = false)
            => ValidateKeyValue(key, maxLength, settings, logger, logPrefix, "key", encoding, mustBeNullTerminated);

        /// <summary>
        /// Validates the given value. Returns either a valid key, or throws a <see cref="MapSaveException"/>, depending on the given file save settings.
        /// Uses ASCII encoding by default.
        /// </summary>
        public static string? ValidateValue(string? value, int? maxLength, FileSaveSettings settings, ILogger logger, string logPrefix, Encoding? encoding = null, bool mustBeNullTerminated = false)
            => ValidateKeyValue(value, maxLength, settings, logger, logPrefix, "value", encoding, mustBeNullTerminated);

        private static string? ValidateKeyValue(string? value, int? maxLength, FileSaveSettings settings, ILogger logger, string logPrefix, string type, Encoding? encoding = null, bool mustBeNullTerminated = false)
        {
            if (value == null)
                return null;

            if (maxLength != null)
            {
                encoding = encoding ?? Encoding.ASCII;
                var rawValue = encoding.GetBytes(value);
                if (mustBeNullTerminated && rawValue.Length == 0 || rawValue[rawValue.Length - 1] != 0)
                    rawValue = rawValue.Append((byte)0).ToArray();

                if (rawValue.Length > maxLength)
                {
                    if (settings.KeyValueTooLongHandling == ValueTooLongHandling.Truncate)
                    {
                        logger.Warning($"{logPrefix} contains a {type} that is too long: '{value}', truncating {type}.");

                        if (mustBeNullTerminated)
                            rawValue[maxLength.Value - 1] = 0;
                        value = encoding.GetString(rawValue, 0, maxLength.Value);
                    }
                    else if (settings.KeyValueTooLongHandling == ValueTooLongHandling.Fail)
                    {
                        var errorMessage = $"{logPrefix} contains a {type} that is too long: '{value}'.";
                        logger.Error(errorMessage);
                        throw new MapSaveException(errorMessage);
                    }
                }
            }

            if (value.Contains('"'))
            {
                if (settings.KeyValueInvalidCharacterHandling == InvalidCharacterHandling.Replace)
                {
                    var newValue = value.Replace("\"", settings.KeyValueInvalidCharacterReplacement);
                    logger.Warning($"{logPrefix} contains a {type} with invalid characters: '{value}', replacing with '{newValue}'.");
                    value = newValue;
                }
                else if (settings.KeyValueInvalidCharacterHandling == InvalidCharacterHandling.Ignore)
                {
                    logger.Warning($"{logPrefix} contains a {type} with invalid characters: '{value}', ignoring.");
                }
                else if (settings.KeyValueInvalidCharacterHandling == InvalidCharacterHandling.Fail)
                {
                    var errorMessage = $"{logPrefix} contains a {type} with invalid characters: '{value}'.";
                    logger.Error(errorMessage);
                    throw new MapSaveException(errorMessage);
                }
            }

            return value;
        }
    }
}
