using MScript.Evaluation.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MScript.Evaluation
{
    public class BaseTypes
    {
        /// <summary>
        /// None represents the absence of a value.
        /// </summary>
        public static TypeDescriptor None { get; }

        /// <summary>
        /// A floating-point double-precision number.
        /// </summary>
        public static TypeDescriptor Number { get; }

        /// <summary>
        /// A fixed-length sequence of numbers.
        /// </summary>
        public static TypeDescriptor Vector { get; }

        /// <summary>
        /// A sequence of characters.
        /// </summary>
        public static TypeDescriptor String { get; }

        /// <summary>
        /// A function or method.
        /// </summary>
        public static TypeDescriptor Function { get; }

        /// <summary>
        /// Any is only used in function signatures, and indicates that any of the other types can be used.
        /// </summary>
        public static TypeDescriptor Any { get; }


        static BaseTypes()
        {
            None = new TypeDescriptor(nameof(None));
            Number = new TypeDescriptor(nameof(Number));
            Vector = new TypeDescriptor(nameof(Vector));
            String = new TypeDescriptor(nameof(String));
            Function = new TypeDescriptor(nameof(Function));
            Any = new TypeDescriptor(nameof(Any));

            RegisterMembers(typeof(VectorMembers));
            RegisterMembers(typeof(StringMembers));
        }


        /// <summary>
        /// Creates MethodDescriptors for all public static methods in the given type,
        /// and registers them with the TypeDescriptor that matches the first parameter.
        /// </summary>
        private static void RegisterMembers(Type methodsContainer)
        {
            // NOTE: Indexing is special-cased in Evaluator.
            foreach (var method in methodsContainer.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var thisParameter = method.GetParameters()[0];
                var thisTypeDescriptor = NativeUtils.GetTypeDescriptor(thisParameter.ParameterType);

                if (method.Name.StartsWith("GET_"))
                {
                    thisTypeDescriptor.AddMember(new PropertyDescriptor(
                        method.Name.Substring(4),
                        NativeUtils.GetTypeDescriptor(method.ReturnType),
                        CreateGetter(method)));
                }
                else
                {
                    thisTypeDescriptor.AddMember(new MethodDescriptor(
                        method.Name,
                        NativeUtils.GetTypeDescriptor(method.ReturnType),
                        NativeUtils.CreateFunction(method)));
                }
            }
        }

        private static Func<object, object> CreateGetter(MethodInfo method) => obj => method.Invoke(null, new object[] { obj });


        static class VectorMembers
        {
            // Properties:
            public static double GET_length(double[] self) => self.Length;
            // Position:
            public static double? GET_x(double[] self) => Operations.Index(self, 0);
            public static double? GET_y(double[] self) => Operations.Index(self, 1);
            public static double? GET_z(double[] self) => Operations.Index(self, 2);
            // Angles:
            public static double? GET_pitch(double[] self) => Operations.Index(self, 0);
            public static double? GET_yaw(double[] self) => Operations.Index(self, 1);
            public static double? GET_roll(double[] self) => Operations.Index(self, 2);
            // Color:
            public static double? GET_r(double[] self) => Operations.Index(self, 0);
            public static double? GET_g(double[] self) => Operations.Index(self, 1);
            public static double? GET_b(double[] self) => Operations.Index(self, 2);
            public static double? GET_brightness(double[] self) => Operations.Index(self, 3);


            // Methods:
            public static double[] slice(double[] self, double start, double? end = null, double? step = null)
            {
                // NOTE: If no end index is specified, the rest of the vector is taken (depending on step direction).
                var startIndex = NormalizedIndex((int)start);
                var stepValue = (int)(step ?? 1);
                if (stepValue > 0)
                {
                    var endIndex = NormalizedIndex((int)(end ?? self.Length));
                    if (endIndex <= startIndex || startIndex >= self.Length)
                        return Array.Empty<double>();

                    var result = new List<double>((endIndex - startIndex) / stepValue);
                    for (int i = startIndex; i < endIndex; i += stepValue)
                        result.Add(self[i]);
                    return result.ToArray();
                }
                else if (stepValue < 0)
                {
                    var endIndex = end is null ? -1 : NormalizedIndex((int)end);
                    if (startIndex <= endIndex || endIndex >= self.Length)
                        return Array.Empty<double>();

                    var result = new List<double>((startIndex - endIndex) / -stepValue);
                    for (int i = startIndex; i > endIndex; i += stepValue)
                        result.Add(self[i]);
                    return result.ToArray();
                }
                else
                {
                    // Produce an empty vector if step is 0:
                    return Array.Empty<double>();
                }

                int NormalizedIndex(int index) => index < 0 ? self.Length + index : index;
            }

            public static double[] skip(double[] self, double count) => self.Skip((int)count).ToArray();
            public static double[] take(double[] self, double count) => self.Take((int)count).ToArray();
            public static double[] concat(double[] self, double[] other) => self.Concat(other).ToArray();

            public static double? max(double[] self) => self.Length > 0 ? (double?)self.Max() : null;
            public static double? min(double[] self) => self.Length > 0 ? (double?)self.Min() : null;
            public static double sum(double[] self) => self.Sum();
        }

        static class StringMembers
        {
            // Properties:
            public static double GET_length(string self) => self.Length;


            // Methods:
            /// <summary>
            /// Returns a substring.
            /// Supports negative indexing.
            /// If length is omitted then the rest of the string, starting at offset, is returned.
            /// The returned substring will be shorter than the requested length if there are less characters available.
            /// </summary>
            public static string substr(string self, double offset, double? length = null)
            {
                offset = (int)offset;

                // Take the rest of the string if no length is specified:
                if (length is null)
                    length = (offset < 0) ? -offset : self.Length - offset;
                else
                    length = (int)length;

                if (offset >= self.Length || length <= 0)
                    return "";

                // Negative indexing:
                if (offset < 0)
                {
                    offset = self.Length + offset;
                    if (offset < 0)
                    {
                        length = Math.Max(0, length.Value + offset);
                        offset = 0;
                    }
                }

                if (offset + length > self.Length)
                    length = self.Length - offset;

                return self.Substring((int)offset, (int)length);
            }

            public static bool contains(string self, string str) => self.Contains(str);
            public static bool startswith(string self, string str) => self.StartsWith(str);
            public static bool endswith(string self, string str) => self.EndsWith(str);
            public static string replace(string self, string str, string replacement) => self.Replace(str, replacement);

            public static string trim(string self, string chars = null) => self.Trim(chars?.ToArray());
            public static string trimstart(string self, string chars = null) => self.TrimStart(chars?.ToArray());
            public static string trimend(string self, string chars = null) => self.TrimEnd(chars?.ToArray());

            public static string segment(string self, string delimiter, double index)
            {
                // NOTE: An empty or null delimiter is safe, it just won't cause any splits:
                var segments = self.Split(new string[] { delimiter }, StringSplitOptions.None);

                // Negative indexing:
                index = (int)index;
                if (index < 0)
                    index = segments.Length + index;

                if (index >= 0 && index < segments.Length)
                    return segments[(int)index];
                else
                    return null;
            }
        }
    }
}
