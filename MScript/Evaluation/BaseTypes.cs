using MScript.Evaluation.Types;
using System.Reflection;
using System.Text.RegularExpressions;

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
            None = new TypeDescriptor("none");
            Number = new TypeDescriptor("number");
            String = new TypeDescriptor("string");
            Array = new TypeDescriptor("array");
            Object = new TypeDescriptor("object");
            Function = new TypeDescriptor("function");
            Any = new TypeDescriptor("any");

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

            // Comparisons and checks:
            public static bool equals(string self, string? str, object? ignore_case = null)
                => self.Equals(str, GetStringComparison(ignore_case));

            public static bool contains(string self, string? str, object? ignore_case = null)
                => str is not null ? self.Contains(str, GetStringComparison(ignore_case)) : false;

            public static bool startswith(string self, string? str, object? ignore_case = null)
                => str is not null ? self.StartsWith(str, GetStringComparison(ignore_case)) : false;

            public static bool endswith(string self, string? str, object? ignore_case = null)
                => str is not null ? self.EndsWith(str, GetStringComparison(ignore_case)) : false;

            public static double? index(string self, string str, double? offset = null, object? ignore_case = null)
            {
                var startIndex = Math.Clamp(Operations.GetNormalizedIndex(self.Length, offset is null ? 0 : (int)offset), 0, self.Length);
                var index = self.IndexOf(str, startIndex, GetStringComparison(ignore_case));
                return index != -1 ? index : null;
            }

            public static double? lastindex(string self, string str, double? offset = null, object? ignore_case = null)
            {
                var startIndex = Math.Clamp(Operations.GetNormalizedIndex(self.Length, offset is null ? self.Length - 1 : (int)offset), 0, self.Length);
                var index = self.LastIndexOf(str, startIndex, GetStringComparison(ignore_case));
                return index != -1 ? index : null;
            }

            public static double? count(string self, string str, double? offset = null, object? ignore_case = null)
            {
                var index = Operations.GetNormalizedIndex(self.Length, offset is null ? 0 : (int)offset);
                if (string.IsNullOrEmpty(str))
                    return Math.Max(0, self.Length + 1 - index);

                var stringComparison = GetStringComparison(ignore_case);
                var count = 0;
                while (index != -1)
                {
                    index = self.IndexOf(str, index, stringComparison);
                    if (index == -1)
                        break;

                    count += 1;
                    index += str.Length;
                }
                return count;
            }

            // Substrings, trimming and replacing:
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

            public static string trim(string self, string? chars = null)
                => self.Trim(chars?.ToArray());

            public static string trimstart(string self, string? chars = null)
                => self.TrimStart(chars?.ToArray());

            public static string trimend(string self, string? chars = null)
                => self.TrimEnd(chars?.ToArray());

            public static string replace(string self, string? str, string? replacement, object? ignore_case = null)
                => !string.IsNullOrEmpty(str) ? self.Replace(str, replacement, GetStringComparison(ignore_case)) : self;

            // Splitting and joining:
            public static object?[] split(string self, object? delimiters = null, double? count = null)
            {
                var splitPattern = @"\s+";
                if (delimiters is string delimiter)
                    splitPattern = Regex.Escape(delimiter);
                else if (delimiters is object?[] delimitersArray)
                    splitPattern = string.Join("|", delimitersArray.Select(Operations.ToString));

                return Split(self, splitPattern, count);
            }

            public static object?[] splitr(string self, string? pattern, double? count = null) => Split(self, pattern ?? @"\s+", count);

            public static string join(string self, object?[] values)
                => string.Join(self, values.Select(value => Operations.ToString(value)));

            // Regular expressions:
            public static bool match(string self, string? pattern) => !string.IsNullOrEmpty(pattern) && Regex.IsMatch(self, pattern);

            public static object?[] matches(string self, string? pattern)
            {
                if (string.IsNullOrEmpty(pattern))
                    return System.Array.Empty<object?>();

                return Regex.Matches(self, pattern)
                    .Select(match => match.Value)
                    .Cast<object?>()
                    .ToArray();
            }


            private static StringComparison GetStringComparison(object? ignore_case)
                => Operations.IsTrue(ignore_case) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            private static object?[] Split(string str, string pattern, double? count)
            {
                var maxParts = count is null ? int.MaxValue : (int)count.Value;
                if (Math.Abs(maxParts) <= 1)
                    return new object?[] { str };

                var splitMatches = Regex.Matches(str, pattern);
                if (splitMatches.Count == 0)
                    return new object?[] { str };

                var partCount = Math.Min(splitMatches.Count + 1, Math.Abs(maxParts));
                var startIndex = maxParts < 0 ? Math.Max(splitMatches.Count + 1 - -maxParts, 0) : 0;

                var parts = new object?[partCount];
                var offset = 0;
                for (int i = 0; i < partCount - 1; i++)
                {
                    var match = splitMatches[startIndex + i];
                    parts[i] = str.Substring(offset, match.Index - offset);
                    offset = match.Index + match.Length;
                }
                parts[parts.Length - 1] = str.Substring(offset);
                return parts;
            }
        }

        static class ArrayMembers
        {
            // Properties:
            public static double GET_length(object?[] self) => self.Length;

            // Position:
            public static double? GET_x(object?[] self) => Operations.Index(self, 0) as double?;
            public static double? GET_y(object?[] self) => Operations.Index(self, 1) as double?;
            public static double? GET_z(object?[] self) => Operations.Index(self, 2) as double?;

            // Angles:
            public static double? GET_pitch(object?[] self) => Operations.Index(self, 0) as double?;
            public static double? GET_yaw(object?[] self) => Operations.Index(self, 1) as double?;
            public static double? GET_roll(object?[] self) => Operations.Index(self, 2) as double?;

            // Color:
            public static double? GET_r(object?[] self) => Operations.Index(self, 0) as double?;
            public static double? GET_g(object?[] self) => Operations.Index(self, 1) as double?;
            public static double? GET_b(object?[] self) => Operations.Index(self, 2) as double?;
            public static double? GET_brightness(object?[] self) => Operations.Index(self, 3) as double?;


            // Methods:

            // Cutting and combining:
            public static object?[] slice(object?[] self, double start, double? end = null, double? step = null)
            {
                // NOTE: If no end index is specified, the rest of the vector is taken (depending on step direction).
                var startIndex = Operations.GetNormalizedIndex(self.Length, (int)start);
                var stepValue = (int)(step ?? 1);
                if (stepValue > 0)
                {
                    var endIndex = Operations.GetNormalizedIndex(self.Length, (int)(end ?? self.Length));
                    if (endIndex <= startIndex || startIndex >= self.Length)
                        return System.Array.Empty<object?>();

                    var result = new List<object?>((endIndex - startIndex) / stepValue);
                    for (int i = startIndex; i < endIndex; i += stepValue)
                        result.Add(self[i]);
                    return result.ToArray();
                }
                else if (stepValue < 0)
                {
                    var endIndex = end is null ? -1 : Operations.GetNormalizedIndex(self.Length, (int)end);
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

            public static object? first(object?[] self) => self.FirstOrDefault();

            public static object? last(object?[] self) => self.LastOrDefault();

            public static object?[] concat(object?[] self, object?[] other) => self.Concat(other).ToArray();

            public static object?[] prepend(object?[] self, object? value) => self.Prepend(value).ToArray();

            public static object?[] append(object?[] self, object? value) => self.Append(value).ToArray();

            public static object?[] insert(object?[] self, double index, object? value)
            {
                var insertIndex = Operations.GetNormalizedIndex(self.Length, (int)index);
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

            // Searching:
            public static object? contains(object?[] self, object? value) => self.Any(val => Operations.IsTrue(Operations.Equals(val, value)));

            public static double? index(object?[] self, object? value, double? offset = null)
            {
                var startIndex = Math.Max(0, Operations.GetNormalizedIndex(self.Length, offset is null ? 0 : (int)offset));
                for (int i = startIndex; i < self.Length; i++)
                {
                    if (Operations.IsTrue(Operations.Equals(self[i], value)))
                        return i;
                }
                return null;
            }

            public static double? lastindex(object?[] self, object? value, double? offset = null)
            {
                var startIndex = Math.Min(Operations.GetNormalizedIndex(self.Length, offset is null ? self.Length - 1 : (int)offset), self.Length - 1);
                for (int i = startIndex; i >= 0; i--)
                {
                    if (Operations.IsTrue(Operations.Equals(self[i], value)))
                        return i;
                }
                return null;
            }

            // Functional programming:
            public static object?[] map(object?[] self, IFunction selector)
            {
                if (selector.Parameters.Count < 1 || selector.Parameters.Count > 2)
                    throw new InvalidOperationException($"The function given to {nameof(map)} must take 1 (value) or 2 (value, index) arguments, not {selector.Parameters.Count}.");

                var result = new object?[self.Length];
                if (selector.Parameters.Count == 1)
                {
                    for (int i = 0; i < self.Length; i++)
                        result[i] = selector.Apply(new[] { self[i] });
                }
                else
                {
                    for (int i = 0; i < self.Length; i++)
                        result[i] = selector.Apply(new[] { self[i], (double)i });
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

            public static object? reduce(object?[] self, IFunction reducer, object? start_value = null)
            {
                if (reducer.Parameters.Count != 2)
                    throw new InvalidOperationException($"The function given to {nameof(reduce)} must take 2 (result, value) arguments, not {reducer.Parameters.Count}.");

                if (self.Length == 0)
                    return start_value;

                var foldBehavior = start_value is not null;
                var result = foldBehavior ? start_value : self[0];
                for (int i = foldBehavior ? 0 : 1; i < self.Length; i++)
                    result = reducer.Apply(new[] { result, self[i] });
                return result;
            }

            public static object?[] groupby(object?[] self, IFunction key_selector)
            {
                if (key_selector.Parameters.Count != 1)
                    throw new InvalidOperationException($"The function given to {nameof(groupby)} must take 1 (value) argument, not {key_selector.Parameters.Count}.");

                var nullKey = new object();
                var groups = new Dictionary<object, List<object?>>(MScriptValueEqualityComparer.Instance);
                for (int i = 0; i < self.Length; i++)
                {
                    var key = key_selector.Apply(new[] { self[i] }) ?? nullKey;
                    if (groups.TryGetValue(key, out var list))
                        list.Add(self[i]);
                    else
                        groups[key] = new List<object?> { self[i] };
                }

                return groups
                    .Select(kv => new MObject(new Dictionary<string, object?> {
                        ["key"] = kv.Key == nullKey ? null : kv.Key,
                        ["values"] = kv.Value.ToArray(),
                    }))
                    .ToArray();
            }

            public static object?[] zip(object?[] self, object?[] other, IFunction zipper)
            {
                if (zipper.Parameters.Count != 2)
                    throw new InvalidOperationException($"The function given {nameof(zip)} must take 2 arguments, not {zipper.Parameters.Count}.");

                var result = new object?[Math.Min(self.Length, other.Length)];
                for (int i = 0; i < result.Length; i++)
                    result[i] = zipper.Apply(new[] { self[i], other[i] });
                return result;
            }

            public static object?[] sort(object?[] self, IFunction sort_by) => self.OrderBy(value => sort_by.Apply(new[] { value }) is double number ? number : 0.0).ToArray();   // TODO: Argument count check!!!

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

            // Numerical:
            public static double? max(object?[] self, IFunction? selector = null)
            {
                var getNumber = CreateNumberSelector(selector);
                double? max = null;
                for (int i = 0; i < self.Length; i++)
                {
                    var value = getNumber(self, i);
                    if (value == null)
                        continue;

                    if (max == null || value.Value > max.Value)
                        max = value;
                }
                return max;
            }

            public static double? min(object?[] self, IFunction? selector = null)
            {
                var getNumber = CreateNumberSelector(selector);
                double? min = null;
                for (int i = 0; i < self.Length; i++)
                {
                    var value = getNumber(self, i);
                    if (value == null)
                        continue;

                    if (min == null || value.Value < min.Value)
                        min = value;
                }
                return min;
            }

            public static double? sum(object?[] self, IFunction? selector = null)
            {
                var getNumber = CreateNumberSelector(selector);
                var sum = 0.0;
                for (int i = 0; i < self.Length; i++)
                {
                    var value = getNumber(self, i);
                    if (value != null)
                        sum += value.Value;
                }
                return sum;
            }


            private static Func<object?[], int, double?> CreateNumberSelector(IFunction? selector = null)
            {
                if (selector == null)
                {
                    return (array, index) => array[index] as double?;
                }
                else
                {
                    if (selector.Parameters.Count != 1)
                        throw new InvalidOperationException($"A numerical selector must take 1 (value) arguments, not {selector.Parameters.Count}.");

                    return (array, index) => selector.Apply(new[] { array[index] }) as double?;
                }
            }
        }
    }
}
