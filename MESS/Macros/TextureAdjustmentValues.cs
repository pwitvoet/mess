using MESS.Common;
using MESS.Logging;
using MESS.Mathematics.Spatial;
using MScript.Evaluation;
using MScript;

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
}
