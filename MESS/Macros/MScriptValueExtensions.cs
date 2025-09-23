using MESS.Mapping;
using MESS.Mathematics.Spatial;
using MScript;
using MScript.Evaluation;

namespace MESS.Macros
{
    static class MScriptValueExtensions
    {
        /// <summary>
        /// Returns the string representation of the specified value, or null if it does not exist.
        /// </summary>
        public static string? GetString(this IDictionary<string, object?> mscriptValues, string name)
            => mscriptValues.TryGetValue(name, out var value)
            ? Interpreter.Print(value)
            : null;

        /// <summary>
        /// Returns the specified number, or null if it does not exist or if the specified value is not a number.
        /// </summary>
        public static double? GetDouble(this IDictionary<string, object?> mscriptValues, string name)
            => mscriptValues.TryGetValue(name, out var value) && TryConvertToDouble(value, out var number)
            ? number
            : null;

        /// <summary>
        /// Returns the specified number as an integer, or null if it does not exist or if the specified value is not a number.
        /// </summary>
        public static int? GetInteger(this IDictionary<string, object?> mscriptValues, string name)
            => mscriptValues.TryGetValue(name, out var value) && TryConvertToDouble(value, out var number)
            ? (int)number
            : null;

        /// <summary>
        /// Returns the specified array as a 3D vector, or null if it does not exist or if the specified value is not an array that contains 3 numbers.
        /// </summary>
        public static Vector3D? GetVector3D(this IDictionary<string, object?> mscriptValues, string name)
            => mscriptValues.TryGetValue(name, out var value) && TryConvertToNumericalArray(value, out var array) && array.Length == 3
            ? new Vector3D((float)array[0], (float)array[1], (float)array[2])
            : null;

        /// <summary>
        /// Returns the specified array as an angles object, or null if it does not exist or if the specified value is not an array that contains 3 numbers.
        /// </summary>
        public static Angles? GetAngles(this IDictionary<string, object?> mscriptValues, string name)
            => mscriptValues.TryGetValue(name, out var value) && TryConvertToNumericalArray(value, out var array) && array.Length == 3
            ? new Angles((float)array[2], (float)array[0], (float)array[1])
            : null;


        /// <summary>
        /// Overwrites the specified value with the given number.
        /// </summary>
        public static void SetDouble(this IDictionary<string, object?> mscriptValues, string name, double value)
            => mscriptValues[name] = value;

        /// <summary>
        /// Overwrites the specified value with the given integer. The integer is converted to an MScript number (double).
        /// </summary>
        public static void SetInteger(this IDictionary<string, object?> mscriptValues, string name, int value)
            => mscriptValues[name] = (double)value;

        /// <summary>
        /// Overwrites the specified value with the given 3D vector. The vector is converted to an MScript array (object?[]).
        /// </summary>
        public static void SetVector3D(this IDictionary<string, object?> mscriptValues, string name, Vector3D value)
            => mscriptValues[name] = new object?[] { (double)value.X, (double)value.Y, (double)value.Z };

        /// <summary>
        /// Overwrites the specified value with the given angles object. The angles are converted to an MScript array (object?[]).
        /// </summary>
        public static void SetAngles(this IDictionary<string, object?> mscriptValues, string name, Angles value)
            => mscriptValues[name] = new object?[] { (double)value.Pitch, (double)value.Yaw, (double)value.Roll };


        /// <summary>
        /// Creates a MScript object from the given face, of the form {'texture': string, 'offset': array, 'angle': number, 'scale': array, 'normal': array},
        /// where each array contains 2 or 3 numbers, depending on the corresponding vector type.
        /// </summary>
        public static MObject CreateFaceInfoMObject(this Face face)
        {
            return new MObject(new[] {
                new KeyValuePair<string, object?>("texture", face.TextureName),
                new KeyValuePair<string, object?>("offset", new object?[] { (double)face.TextureShift.X, (double)face.TextureShift.Y }),
                new KeyValuePair<string, object?>("angle", (double)face.TextureAngle),
                new KeyValuePair<string, object?>("scale", new object?[] { (double)face.TextureScale.X, (double)face.TextureScale.Y }),
                new KeyValuePair<string, object?>("normal", new object?[] { (double)face.Plane.Normal.X, (double)face.Plane.Normal.Y, (double)face.Plane.Normal.Z }),
            });
        }


        private static bool TryConvertToDouble(object? mscriptValue, out double number)
        {
            if (mscriptValue is double d)
            {
                number = d;
                return true;
            }
            else if (mscriptValue is string str)
            {
                return PropertyExtensions.TryParseDouble(str, out number);
            }

            number = 0;
            return false;
        }

        private static bool TryConvertToNumericalArray(object? mscriptValue, out double[] array)
        {
            if (mscriptValue is object?[] mscriptArray)
            {
                array = Array.Empty<double>();
                foreach (var item in mscriptArray)
                    if (item is not double)
                        return false;

                array = new double[mscriptArray.Length];
                for (int i = 0; i < mscriptArray.Length; i++)
                    array[i] = (double)mscriptArray[i]!;
                return true;
            }
            else if (mscriptValue is string str)
            {
                return PropertyExtensions.TryParseNumericalArray(str, out array);
            }

            array = Array.Empty<double>();
            return false;
        }
    }
}
