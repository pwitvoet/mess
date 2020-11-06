using MScript.Evaluation.Types;
using System;
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


        static BaseTypes()
        {
            None = new TypeDescriptor(nameof(None));
            Number = new TypeDescriptor(nameof(Number));
            Vector = new TypeDescriptor(nameof(Vector));
            String = new TypeDescriptor(nameof(String));
            Function = new TypeDescriptor(nameof(Function));

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
            // TODO: Take a slice ('substring')!
            // TODO: Append/prepend/concat?
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
        }
    }
}
