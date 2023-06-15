using MScript.Evaluation.Types;
using System.Reflection;

namespace MScript.Evaluation
{
    public static class NativeUtils
    {
        /// <summary>
        /// Creates functions for all public static methods in the given type, and creates bindings for them in the given context.
        /// </summary>
        public static void RegisterStaticMethods(EvaluationContext context, Type functionsContainer)
        {
            foreach (var method in functionsContainer.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var function = CreateFunction(method);
                context.Bind(function.Name, function);
            }
        }

        /// <summary>
        /// Creates functions for all public instance methods in the given instance, and creates bindings for them in the given context.
        /// </summary>
        public static void RegisterInstanceMethods(EvaluationContext context, object instance)
        {
            foreach (var method in instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var function = CreateFunction(method, instance);
                context.Bind(function.Name, function);
            }
        }

        /// <summary>
        /// Creates an MScript function that calls a 'native' method.
        /// <para>
        /// All parameters must be MScript-compatible types
        /// (<see cref="double"/>, <see cref="double?"/>, <see cref="object?[]"/>, <see cref="string"/>, <see cref="MObject"/>, <see cref="IFunction"/> or <see cref="object"/>).
        /// The return type can also be <see cref="bool"/>. Parameters of type <see cref="object"/> are not type-checked by MScript.
        /// Optional parameters are also optional in MScript.
        /// </para>
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="InvalidOperationException"/>
        public static NativeFunction CreateFunction(MethodInfo method, object? instance = null)
        {
            if (!method.IsStatic && instance == null)
                throw new InvalidOperationException($"A member function requires an instance.");

            if (method.IsStatic)
                instance = null;

            var parameters = method.GetParameters()
                .Select(param => new Parameter(param.Name ?? "", GetTypeDescriptor(param.ParameterType), param.IsOptional, ConvertDefaultValue(param.DefaultValue)))
                .ToArray();

            return new NativeFunction(
                method.Name,
                parameters,
                arguments => ConvertResult(method.Invoke(instance, arguments)));
        }

        /// <summary>
        /// Returns the MScript type descriptor for the given 'native' type, if the type is supported.
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        public static TypeDescriptor GetTypeDescriptor(Type type)
        {
            if (type == typeof(double) || type == typeof(double?))
                return BaseTypes.Number;
            else if (type == typeof(object?[]))
                return BaseTypes.Array;
            else if (type == typeof(string))
                return BaseTypes.String;
            else if (type == typeof(MObject))
                return BaseTypes.Object;
            else if (typeof(IFunction).IsAssignableFrom(type))
                return BaseTypes.Function;
            else if (type == typeof(bool) || type == typeof(object))
                return BaseTypes.Any;

            throw new NotSupportedException($"No type descriptor for {type.FullName}.");
        }


        /// <summary>
        /// Helper method that converts 'native' boolean results to an MScript-compatible result.
        /// Any other result is returned as-is.
        /// </summary>
        private static object? ConvertResult(object? result) => result switch
        {
            true => 1.0,
            false => null,
            _ => result,
        };

        private static object? ConvertDefaultValue(object? value) => value switch
        {
            double number => number,
            string str => str,
            object?[] array => array,
            MObject obj => obj,
            _ => null,
        };
    }
}
