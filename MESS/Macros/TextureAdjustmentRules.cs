using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript.Evaluation;
using System.Diagnostics.CodeAnalysis;

namespace MESS.Macros
{
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
        /// Apply these texture adjustment rules to the given brush. This will modify face properties in-place.
        /// </summary>
        public void ApplyToBrush(Brush brush, ILogger logger)
        {
            if (!NamedRuleValues.Any() && DefaultValues is null)
                return;


            foreach (var face in brush.Faces)
                ApplyToFace(face, logger);
        }

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
                    isTextureOffsetSet = SetFaceProperty(adjustmentValues.OffsetFunction, adjustmentValues.Offset, TextureAdjustmentValues.ConvertToVector2, offset => face.TextureShift += offset, faceInfo, "texture offset", logger);

                if (!isTextureAngleSet)
                    isTextureAngleSet = SetFaceProperty(adjustmentValues.AngleFunction, adjustmentValues.Angle, TextureAdjustmentValues.ConvertToFloat, angle => ApplyNewAngle(face, face.TextureAngle + angle), faceInfo, "texture angle", logger);

                if (!isTextureScaleSet)
                    isTextureScaleSet = SetFaceProperty(adjustmentValues.ScaleFunction, adjustmentValues.Scale, TextureAdjustmentValues.ConvertToVector2, scale => face.TextureScale = new Vector2D(face.TextureScale.X * scale.X, face.TextureScale.Y * scale.Y), faceInfo, "texture scale", logger);


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
