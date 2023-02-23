using MScript.Evaluation.Types;
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
        /// A sequence of characters.
        /// </summary>
        public static TypeDescriptor String { get; }

        /// <summary>
        /// A fixed-length sequence of values.
        /// </summary>
        public static TypeDescriptor Array { get; }

        /// <summary>
        /// An immutable object that contains a collection of names and associated values.
        /// </summary>
        public static TypeDescriptor Object { get; }

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
            String = new TypeDescriptor(nameof(String));
            Array = new TypeDescriptor(nameof(Array));
            Object = new TypeDescriptor(nameof(Object));
            Function = new TypeDescriptor(nameof(Function));
            Any = new TypeDescriptor(nameof(Any));

            RegisterMembers(typeof(StringMembers));
            RegisterMembers(typeof(ArrayMembers));
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

        private static Func<object?, object?> CreateGetter(MethodInfo method) => obj => method.Invoke(null, new[] { obj });


        static class StringMembers
        {
            // Properties:
            public static double GET_length(string self) => self.Length;


            // Methods:
            public static bool equals(string self, string? str, double? mode = null)
            {
                var comparisonType = mode is double d && d == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return self.Equals(str, comparisonType);
            }

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

            public static bool contains(string self, string? str) => str is not null ? self.Contains(str) : false;
            public static double? index(string self, string str, double? offset = null)
            {
                var startIndex = NormalizedIndex(self, offset is null ? 0 : (int)offset);
                var index = self.IndexOf(str, startIndex);
                return index != -1 ? index : null;
            }
            public static double? lastindex(string self, string str, double? offset = null)
            {
                var startIndex = NormalizedIndex(self, offset is null ? 0 : (int)offset);
                var index = self.LastIndexOf(str, startIndex);
                return index != -1 ? index : null;
            }
            public static bool startswith(string self, string? str) => str is not null ? self.StartsWith(str) : false;
            public static bool endswith(string self, string? str) => str is not null ? self.EndsWith(str) : false;
            public static string replace(string self, string? str, string? replacement) => str is not null ? self.Replace(str, replacement) : self;

            public static string trim(string self, string? chars = null) => self.Trim(chars?.ToArray());
            public static string trimstart(string self, string? chars = null) => self.TrimStart(chars?.ToArray());
            public static string trimend(string self, string? chars = null) => self.TrimEnd(chars?.ToArray());

            public static object?[] split(string self, string? delimiter = null) => self.Split(delimiter ?? " ", StringSplitOptions.None).Cast<object?>().ToArray();
            public static string join(string self, object?[] array) => string.Join(self, array.Select(value => Operations.ToString(value)));


            private static int NormalizedIndex(string str, int index) => index < 0 ? str.Length + index : index;
        }

        static class ArrayMembers
        {
            // Properties:
            public static double GET_length(object?[] self) => self.Length;
            // Position:
            public static object? GET_x(object?[] self) => Operations.Index(self, 0);
            public static object? GET_y(object?[] self) => Operations.Index(self, 1);
            public static object? GET_z(object?[] self) => Operations.Index(self, 2);
            // Angles:
            public static object? GET_pitch(object?[] self) => Operations.Index(self, 0);
            public static object? GET_yaw(object?[] self) => Operations.Index(self, 1);
            public static object? GET_roll(object?[] self) => Operations.Index(self, 2);
            // Color:
            public static object? GET_r(object?[] self) => Operations.Index(self, 0);
            public static object? GET_g(object?[] self) => Operations.Index(self, 1);
            public static object? GET_b(object?[] self) => Operations.Index(self, 2);
            public static object? GET_brightness(object?[] self) => Operations.Index(self, 3);


            // Methods:
            public static object?[] slice(object?[] self, double start, double? end = null, double? step = null)
            {
                // NOTE: If no end index is specified, the rest of the vector is taken (depending on step direction).
                var startIndex = NormalizedIndex(self, (int)start);
                var stepValue = (int)(step ?? 1);
                if (stepValue > 0)
                {
                    var endIndex = NormalizedIndex(self, (int)(end ?? self.Length));
                    if (endIndex <= startIndex || startIndex >= self.Length)
                        return System.Array.Empty<object?>();

                    var result = new List<object?>((endIndex - startIndex) / stepValue);
                    for (int i = startIndex; i < endIndex; i += stepValue)
                        result.Add(self[i]);
                    return result.ToArray();
                }
                else if (stepValue < 0)
                {
                    var endIndex = end is null ? -1 : NormalizedIndex(self, (int)end);
                    if (startIndex <= endIndex || endIndex >= self.Length)
                        return System.Array.Empty<object?>();

                    var result = new List<object?>((startIndex - endIndex) / -stepValue);
                    for (int i = startIndex; i > endIndex; i += stepValue)
                        result.Add(self[i]);
                    return result.ToArray();
                }
                else
                {
                    // Produce an empty vector if step is 0:
                    return System.Array.Empty<object?>();
                }
            }

            public static object?[] skip(object?[] self, double count) => self.Skip((int)count).ToArray();
            public static object?[] take(object?[] self, double count) => self.Take((int)count).ToArray();
            public static object?[] concat(object?[] self, object?[] other) => self.Concat(other).ToArray();
            public static object?[] prepend(object?[] self, object? value) => self.Prepend(value).ToArray();
            public static object?[] append(object?[] self, object? value) => self.Append(value).ToArray();
            public static object?[] insert(object?[] self, double index, object? value)
            {
                var insertIndex = NormalizedIndex(self, (int)index);
                if (insertIndex < 0)
                    insertIndex = 0;
                else if (insertIndex > self.Length)
                    insertIndex = self.Length;

                var result = new object?[self.Length + 1];
                for (int i = 0; i < insertIndex; i++)
                    result[i] = self[i];

                result[insertIndex] = value;
                for (int i = insertIndex; i < self.Length; i++)
                    result[i + 1] = self[i];

                return result;
            }

            public static object? contains(object?[] self, object? value) => self.Any(val => Operations.IsTrue(Operations.Equals(val, value)));
            public static double? indexof(object?[] self, object? value)
            {
                for (int i = 0; i < self.Length; i++)
                {
                    if (Operations.IsTrue(Operations.Equals(self[i], value)))
                        return i;
                }
                return null;
            }

            public static object?[] map(object?[] self, IFunction function)
            {
                if (function.Parameters.Count < 1 || function.Parameters.Count > 2)
                    throw new InvalidOperationException($"The function given to {nameof(map)} must take 1 (value) or 2 (value, index) arguments, not {function.Parameters.Count}.");

                var result = new object?[self.Length];
                if (function.Parameters.Count == 1)
                {
                    for (int i = 0; i < self.Length; i++)
                        result[i] = function.Apply(new[] { self[i] });
                }
                else
                {
                    for (int i = 0; i < self.Length; i++)
                        result[i] = function.Apply(new[] { self[i], (double)i });
                }
                return result;
            }
            public static object?[] filter(object?[] self, IFunction predicate)
            {
                if (predicate.Parameters.Count < 1 || predicate.Parameters.Count > 2)
                    throw new InvalidOperationException($"The function given to {nameof(filter)} must take 1 (value) or 2 (value, index) arguments, not {predicate.Parameters.Count}.");

                var result = new List<object?>();
                if (predicate.Parameters.Count == 1)
                {
                    for (int i = 0; i < self.Length; i++)
                    {
                        if (Operations.IsTrue(predicate.Apply(new[] { self[i] })))
                            result.Add(self[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < self.Length; i++)
                    {
                        if (Operations.IsTrue(predicate.Apply(new[] { self[i], (double)i })))
                            result.Add(self[i]);
                    }
                }
                return result.ToArray();
            }
            public static object? reduce(object?[] self, IFunction function, object? startValue = null)
            {
                if (self.Length == 0)
                    return startValue;

                var foldBehavior = startValue is not null;
                var result = foldBehavior ? startValue : self[0];
                for (int i = foldBehavior ? 0 : 1; i < self.Length; i++)
                    result = function.Apply(new[] { result, self[i] });
                return result;
            }
            public static object?[] zip(object?[] self, object?[] other, IFunction function)
            {
                if (function.Parameters.Count != 2)
                    throw new InvalidOperationException($"The function given {nameof(zip)} must take 2 arguments, not {function.Parameters.Count}.");

                var result = new object?[Math.Min(self.Length, other.Length)];
                for (int i = 0; i < result.Length; i++)
                    result[i] = function.Apply(new[] { self[i], other[i] });
                return result;
            }
            public static object?[] sort(object?[] self, IFunction sortby) => self.OrderBy(value => sortby.Apply(new[] { value }) is double number ? number : 0.0).ToArray();
            public static object?[] reverse(object?[] self) => self.Reverse().ToArray();
            public static bool any(object?[] self, IFunction? predicate = null)
            {
                if (predicate == null)
                    return self.Any();

                if (predicate.Parameters.Count != 1)
                    throw new InvalidOperationException($"The function given to {nameof(any)} must take 1 (value) argument, not {predicate.Parameters.Count}.");

                return self.Any(value => Operations.IsTrue(predicate.Apply(new[] { value })));
            }
            public static bool all(object?[] self, IFunction predicate)
            {
                if (predicate.Parameters.Count != 1)
                    throw new InvalidOperationException($"The function given to {nameof(any)} must take 1 (value) argument, not {predicate.Parameters.Count}.");

                return self.All(value => Operations.IsTrue(predicate.Apply(new[] { value })));
            }

            // Numerical arrays only:
            public static double? max(object?[] self, IFunction? selector = null) => ReduceToNumber(self, Math.Max, selector);
            public static double? min(object?[] self, IFunction? selector = null) => ReduceToNumber(self, Math.Min, selector);
            public static double? sum(object?[] self, IFunction? selector = null) => ReduceToNumber(self, (sum, number) => sum + number, selector);


            private static double? ReduceToNumber(object?[] array, Func<double, double, double> aggregate, IFunction? selector = null)
            {
                var result = 0.0;
                for (int i = 0; i < array.Length; i++)
                {
                    var value = selector != null ? selector.Apply(new[] { array[i] }) : array[i];
                    if (value is null)
                        result = aggregate(result, 0.0);
                    else if (value is double number)
                        result = aggregate(result, number);
                    else
                        return null;
                }
                return result;
            }

            private static int NormalizedIndex(object?[] array, int index) => index < 0 ? array.Length + index : index;
        }
    }
}
