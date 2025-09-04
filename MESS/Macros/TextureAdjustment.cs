using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using MScript.Evaluation;
using System.Diagnostics.CodeAnalysis;

namespace MESS.Macros
{
    /// <summary>
    /// A set of values and value-providing functions, which can be used to change the various texture-related properties of a face.
    /// </summary>
    public class TextureAdjustmentValues
    {
        public bool HasFunctions =>
            AdjustmentValuesFunction is not null ||
            TextureNameFunction is not null ||
            OffsetFunction is not null ||
            AngleFunction is not null ||
            ScaleFunction is not null;

        public IFunction? AdjustmentValuesFunction { get; set; }
        public IFunction? TextureNameFunction { get; set; }
        public IFunction? OffsetFunction { get; set; }
        public IFunction? AngleFunction { get; set; }
        public IFunction? ScaleFunction { get; set; }

        public string? TextureName { get; set; }
        public Vector2D? Offset { get; set; }
        public float? Angle { get; set; }
        public Vector2D? Scale { get; set; }


        public void StoreValue(string key, object? value, ILogger logger)
        {
            if (value is IFunction function)
            {
                StoreFunction(key, function);
            }
            else
            {
                try
                {
                    switch (key)
                    {
                        case Attributes.ReplaceTexture: TextureName = ConvertToString(value); break;
                        case Attributes.ShiftTexture: Offset = ConvertToVector2(value); break;
                        case Attributes.RotateTexture: Angle = ConvertToFloat(value); break;
                        case Attributes.ScaleTexture: Scale = ConvertToVector2(value); break;

                        case Attributes.AdjustTexture:
                            if (value is not MObject mObject)
                                throw new InvalidDataException("Adjustment value must be an object.");

                            if (mObject.Fields.TryGetValue(TextureAdjustmentRules.TextureNameKey, out var textureNameValue))
                                StoreValue(Attributes.ReplaceTexture, textureNameValue, logger);

                            if (mObject.Fields.TryGetValue(TextureAdjustmentRules.TextureOffsetKey, out var textureOffsetValue))
                                StoreValue(Attributes.ShiftTexture, textureOffsetValue, logger);

                            if (mObject.Fields.TryGetValue(TextureAdjustmentRules.TextureAngleKey, out var textureAngleValue))
                                StoreValue(Attributes.RotateTexture, textureAngleValue, logger);

                            if (mObject.Fields.TryGetValue(TextureAdjustmentRules.TextureScaleKey, out var textureScaleValue))
                                StoreValue(Attributes.ScaleTexture, textureScaleValue, logger);

                            break;
                    }
                }
                catch (Exception ex)
                {
                    var targetType = key switch
                    {
                        Attributes.ReplaceTexture => "string",
                        Attributes.ShiftTexture => "array of numbers",
                        Attributes.RotateTexture => "number",
                        Attributes.ScaleTexture => "array of numbers",
                        _ => "?",
                    };
                    logger.Warning($"Failed to convert {key} value '{value}' to {targetType}, value will be ignored.", ex);
                }
            }
        }

        public void StoreFunction(string key, IFunction function)
        {
            switch (key)
            {
                case Attributes.ReplaceTexture: TextureNameFunction = function; break;
                case Attributes.ShiftTexture: OffsetFunction = function; break;
                case Attributes.RotateTexture: AngleFunction = function; break;
                case Attributes.ScaleTexture: ScaleFunction = function; break;
                case Attributes.AdjustTexture: AdjustmentValuesFunction = function; break;
            }
        }


        public static string? ConvertToString(object? mscriptValue)
            => mscriptValue is null ? null : Interpreter.Print(mscriptValue);

        public static float? ConvertToFloat(object? mscriptValue)
        {
            switch (mscriptValue)
            {
                case null: return null;
                case double number: return (float)number;
                default: throw new InvalidDataException($"Cannot convert {mscriptValue.GetType().Name} to a floating point number.");
            }
        }

        public static Vector2D? ConvertToVector2(object? mscriptValue)
        {
            if (mscriptValue is null)
                return null;

            // NOTE: This can be simplified in C# 11 (if (mscriptValue is [double x, double y])):
            if (mscriptValue is object?[] array && array.Length == 2 && array[0] is double d1 && array[1] is double d2)
                return new Vector2D((float)d1, (float)d2);

            throw new InvalidDataException($"Cannot convert {mscriptValue.GetType().Name} to a {nameof(Vector2D)}.");
        }
    }


    public class TextureAdjustmentRules
    {
        public const string TextureNameKey = "texture";
        public const string TextureOffsetKey = "offset";
        public const string TextureAngleKey = "angle";
        public const string TextureScaleKey = "scale";


        /// <summary>
        /// Read texture adjustment rules from the given (evaluated) entity properties.
        /// Any texture-related special properties will be removed from the given dictionary.
        /// </summary>
        public static TextureAdjustmentRules? GetFromProperties(IDictionary<string, object?> evaluatedProperties, ILogger logger)
        {
            // NOTE: Special texture keys are processed in the following order:
            //       1. Named rules             "_mess_###_texture NAME": "VALUE"
            //       2. Named 'adjust' rules    "_mess_adjust_texture NAME": "VALUE"
            //       3. Default rules           "_mess_###_texture": "VALUE"
            //       4. Default 'adjust' rules  "_mess_adjust_texture": "VALUE"
            //
            //       This ensures that values provided by 'adjust' rules will override values provided by other rules.
            //       Also, unnamed keys that have an MScript object as value are treated as a group of named rules,
            //       so by treating default rules last, these named rule groups will override named keys.

            var specialTextureProperties = evaluatedProperties
                .Where(property => IsSpecialTextureProperty(property.Key))
                .OrderBy(property => property.Key.Contains(' ') ? 0 : 2 + (property.Key.StartsWith(Attributes.AdjustTexture) ? 1 : 0))
                .ToArray();

            if (!specialTextureProperties.Any())
                return null;


            var adjustmentRules = new TextureAdjustmentRules();
            foreach (var property in specialTextureProperties)
            {
                evaluatedProperties.Remove(property.Key);

                var key = property.Key;
                if (IsNamedRule(property.Key))
                {
                    // This is a named rule (a key/value pair of the form "_mess_###_texture NAME": "VALUE"):
                    (key, var textureName) = GetNamedRuleParts(property.Key);
                    textureName = textureName.ToLowerInvariant();

                    GetNamedRuleAdjustmentValues(textureName).StoreValue(key, property.Value, logger);
                }
                else
                {
                    if (IsNamedRuleGroupValue(key, property.Value, out var mObject))
                    {
                        // This is a group of named rules (a key/value pair of the form "_mess_###_texture": "{{NAME1: VALUE1, NAME2: VALUE2, ...}}"):
                        foreach (var field in mObject.Fields)
                        {
                            var textureName = field.Key.ToLowerInvariant();
                            var adjustmentValues = string.IsNullOrEmpty(textureName) ? GetDefaultAdjustmentValues() : GetNamedRuleAdjustmentValues(textureName);
                            adjustmentValues.StoreValue(key, field.Value, logger);
                        }
                    }
                    else
                    {
                        // This is a default rule (a key/value pair of the form "_mess_###_texture": "VALUE"):
                        GetDefaultAdjustmentValues().StoreValue(key, property.Value, logger);
                    }
                }
            }
            return adjustmentRules;


            // Get and initialize adjustment values:
            TextureAdjustmentValues GetNamedRuleAdjustmentValues(string textureName)
            {
                if (!adjustmentRules.NamedRuleValues.TryGetValue(textureName, out var adjustmentValues))
                {
                    adjustmentValues = new TextureAdjustmentValues();
                    adjustmentRules.NamedRuleValues[textureName] = adjustmentValues;
                }
                return adjustmentValues;
            }

            TextureAdjustmentValues GetDefaultAdjustmentValues()
            {
                if (adjustmentRules.DefaultValues is null)
                    adjustmentRules.DefaultValues = new TextureAdjustmentValues();
                return adjustmentRules.DefaultValues;
            }
        }

        /// <summary>
        /// Determines whether an MScript object value is a group of named rules or a set of values for an 'adjust' rule (which can set multiple face properties).
        /// </summary>
        private static bool IsNamedRuleGroupValue(string key, object? value, [NotNullWhen(true)] out MObject? mObject)
        {
            mObject = value as MObject;
            if (mObject is null)
                return false;

            if (key == Attributes.AdjustTexture)
                return mObject.Fields.Any(field => field.Value is MObject) && mObject.Fields.All(field => field.Value is MObject || field.Value is null);
            else
                return true;
        }

        private static bool IsSpecialTextureProperty(string propertyName)
        {
            switch (propertyName)
            {
                case Attributes.ReplaceTexture:
                case Attributes.ShiftTexture:
                case Attributes.RotateTexture:
                case Attributes.ScaleTexture:
                case Attributes.AdjustTexture:
                    return true;
            }

            if (!IsNamedRule(propertyName))
                return false;

            // All of these properties can be followed by a texture name, which acts as a filter:
            (var key, var textureName) = GetNamedRuleParts(propertyName);
            return IsSpecialTextureProperty(key);
        }

        private static bool IsNamedRule(string propertyName)
            => propertyName.Contains(' ') && propertyName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 2;

        // NOTE: Only call this after calling IsNamedRule!
        private static (string key, string name) GetNamedRuleParts(string propertyName)
        {
            var parts = propertyName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (parts[0], parts[1]);
        }


        public Dictionary<string, TextureAdjustmentValues> NamedRuleValues { get; } = new();
        public TextureAdjustmentValues? DefaultValues { get; private set; }


        /// <summary>
        /// Apply these texture adjustment rules to the given brushes. This will modify their face properties in-place.
        /// </summary>
        public void ApplyToBrushes(IEnumerable<Brush> brushes, ILogger logger)
        {
            if (!NamedRuleValues.Any() && DefaultValues is null)
                return;


            foreach (var brush in brushes)
            {
                foreach (var face in brush.Faces)
                    ApplyToFace(face, logger);
            }
        }

        /// <summary>
        /// Apply these texture adjustment rules to the given face. This will modify the face properties in-place.
        /// </summary>
        public void ApplyToFace(Face face, ILogger logger)
        {
            var textureName = face.TextureName.ToLowerInvariant();
            if (!NamedRuleValues.TryGetValue(textureName, out var adjustmentValues))
                adjustmentValues = DefaultValues;

            if (adjustmentValues is null)
                return;


            var isTextureNameSet = false;
            var isTextureOffsetSet = false;
            var isTextureAngleSet = false;
            var isTextureScaleSet = false;

            // First use named rules to set values (if available), then use the default rule:
            MObject? faceInfo = null;
            do
            {
                if (adjustmentValues.HasFunctions && faceInfo is null)
                    faceInfo = CreateFaceInfoObject(face);


                // First call the adjustment function (if provided), which can return values for all properties:
                if (adjustmentValues.AdjustmentValuesFunction is not null)
                {
                    var values = adjustmentValues.AdjustmentValuesFunction.Apply(new[] { faceInfo });
                    if (values is MObject valuesObject)
                    {
                        if (!isTextureNameSet && valuesObject.Fields.TryGetValue(TextureNameKey, out var newTextureName))
                        {
                            SetFaceProperty(newTextureName, TextureAdjustmentValues.ConvertToString, name => face.TextureName = name, "texture", logger);
                            isTextureNameSet = true;
                        }

                        if (!isTextureOffsetSet && valuesObject.Fields.TryGetValue(TextureOffsetKey, out var newOffset))
                        {
                            SetFaceProperty(newOffset, TextureAdjustmentValues.ConvertToVector2, offset => face.TextureShift = offset, "texture offset", logger);
                            isTextureOffsetSet = true;
                        }

                        if (!isTextureAngleSet && valuesObject.Fields.TryGetValue(TextureAngleKey, out var newAngle))
                        {
                            SetFaceProperty(newAngle, TextureAdjustmentValues.ConvertToFloat, angle => ApplyNewAngle(face, angle), "texture angle", logger);
                            isTextureAngleSet = true;
                        }

                        if (!isTextureScaleSet && valuesObject.Fields.TryGetValue(TextureScaleKey, out var newScale))
                        {
                            SetFaceProperty(newScale, TextureAdjustmentValues.ConvertToVector2, scale => face.TextureScale = scale, "texture scale", logger);
                            isTextureScaleSet = true;
                        }
                    }
                    else if (values is not null)
                    {
                        logger.Warning($"Texture adjustment function returned {values}, which is not an object. Value will be ignored!");
                    }
                }

                // Next, for each property that has not yet been set, call the associated function if provided, or else use the static value if provided:
                if (!isTextureNameSet)
                    isTextureNameSet = SetFaceProperty(adjustmentValues.TextureNameFunction, adjustmentValues.TextureName, TextureAdjustmentValues.ConvertToString, name => face.TextureName = name, faceInfo, "texture", logger);

                if (!isTextureOffsetSet)
                    isTextureOffsetSet = SetFaceProperty(adjustmentValues.OffsetFunction, adjustmentValues.Offset, TextureAdjustmentValues.ConvertToVector2, offset => face.TextureShift = offset, faceInfo, "texture offset", logger);

                if (!isTextureAngleSet)
                    isTextureAngleSet = SetFaceProperty(adjustmentValues.AngleFunction, adjustmentValues.Angle, TextureAdjustmentValues.ConvertToFloat, angle => ApplyNewAngle(face, angle), faceInfo, "texture angle", logger);

                if (!isTextureScaleSet)
                    isTextureScaleSet = SetFaceProperty(adjustmentValues.ScaleFunction, adjustmentValues.Scale, TextureAdjustmentValues.ConvertToVector2, scale => face.TextureScale = scale, faceInfo, "texture scale", logger);


                // If all properties are set then we're finished:
                if (isTextureNameSet && isTextureOffsetSet && isTextureAngleSet && isTextureScaleSet)
                    break;

                // If a named rule didn't set all properties, try the default rule (if available) for the remaining properties:
                if (adjustmentValues != DefaultValues)
                    adjustmentValues = DefaultValues;
                else
                    break;
            }
            while (adjustmentValues != null);
        }


        private static MObject CreateFaceInfoObject(Face face)
        {
            return new MObject(new[] {
                new KeyValuePair<string, object?>("texture", face.TextureName),
                new KeyValuePair<string, object?>("offset", new object?[] { (double)face.TextureShift.X, (double)face.TextureShift.Y }),
                new KeyValuePair<string, object?>("angle", (double)face.TextureAngle),
                new KeyValuePair<string, object?>("scale", new object?[] { (double)face.TextureScale.X, (double)face.TextureScale.Y }),
                new KeyValuePair<string, object?>("normal", new object?[] { (double)face.Plane.Normal.X, (double)face.Plane.Normal.Y, (double)face.Plane.Normal.Z }),
            });
        }


        private static void ApplyNewAngle(Face face, float newAngle)
        {
            var radians = (newAngle - face.TextureAngle).ToRadians();
            var texturePlaneNormal = face.TextureRightAxis.CrossProduct(face.TextureDownAxis).Normalized();

            face.TextureAngle = newAngle;
            face.TextureRightAxis = face.TextureRightAxis.RotateAroundAxis(texturePlaneNormal, radians);
            face.TextureDownAxis = face.TextureDownAxis.RotateAroundAxis(texturePlaneNormal, radians);
        }


        private static void SetFaceProperty<TValue>(object? value, Func<object?, TValue?> convertValue, Action<TValue> setValue, string propertyName, ILogger logger)
            where TValue : class
        {
            try
            {
                var typedValue = convertValue(value);
                if (typedValue is not null)
                    setValue(typedValue);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to set {propertyName}, value '{value}' could not be converted and will be ignored.", ex);
            }
        }

        private static void SetFaceProperty<TValue>(object? value, Func<object?, TValue?> convertValue, Action<TValue> setValue, string propertyName, ILogger logger)
            where TValue : struct
        {
            try
            {
                var typedValue = convertValue(value);
                if (typedValue.HasValue)
                    setValue(typedValue.Value);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to set {propertyName}, value '{value}' could not be converted and will be ignored.", ex);
            }
        }

        private static bool SetFaceProperty<TValue>(
            IFunction? getValueFunction,
            TValue? staticValue,
            Func<object?, TValue?> convertValue,
            Action<TValue> setValue,
            MObject? faceInfo,
            string propertyName,
            ILogger logger)
            where TValue : class
        {
            var value = staticValue;
            if (getValueFunction is not null)
            {
                try
                {
                    var rawValue = getValueFunction.Apply(new[] { faceInfo });
                    value = convertValue(rawValue);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to set {propertyName}, value '{value}' could not be converted and will be ignored.", ex);
                    return false;
                }
            }

            if (value is null)
                return false;

            setValue(value);
            return true;
        }

        private static bool SetFaceProperty<TValue>(
            IFunction? getValueFunction,
            TValue? staticValue,
            Func<object?, TValue?> convertValue,
            Action<TValue> setValue,
            MObject? faceInfo,
            string propertyName,
            ILogger logger)
            where TValue : struct
        {
            var value = staticValue;
            if (getValueFunction is not null)
            {
                try
                {
                    var rawValue = getValueFunction.Apply(new[] { faceInfo });
                    value = convertValue(rawValue);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to set {propertyName}, value '{value}' could not be converted and will be ignored.", ex);
                    return false;
                }
            }

            if (!value.HasValue)
                return false;

            setValue(value.Value);
            return true;
        }
    }
}
