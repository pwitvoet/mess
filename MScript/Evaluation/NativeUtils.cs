using MScript.Evaluation.Types;
using System;
using System.Linq;
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
        /// (<see cref="double"/>, <see cref="double?"/>, <see cref="double[]"/>, <see cref="string"/>, <see cref="IFunction"/> or <see cref="object"/>).
        /// Additionally, methods can have a first parameter of type <see cref="EvaluationContext"/>.
        /// The return type can also be <see cref="bool"/>. Parameters of type <see cref="object"/> are not type-checked by MScript.
        /// Optional parameters are also optional in MScript.
        /// </para>
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="InvalidOperationException"/>
        public static NativeFunction CreateFunction(MethodInfo method, object instance = null)
        {
            if (!method.IsStatic && instance == null)
                throw new InvalidOperationException($"A member function requires an instance.");

            if (method.IsStatic)
                instance = null;

            // TODO: param.DefaultValue may not be an 'MScript-compatible' type!
            var nativeParameters = method.GetParameters();
            var contextParametersCount = nativeParameters.Count(parameter => parameter.ParameterType == typeof(EvaluationContext));
            if (contextParametersCount > 0 && nativeParameters[0].ParameterType != typeof(EvaluationContext))
                throw new InvalidOperationException($"Methods that accept an {typeof(EvaluationContext)} must have that as their first parameter.");
            else if (contextParametersCount > 1)
                throw new InvalidOperationException($"Only one {typeof(EvaluationContext)} parameter is allowed.");

            var parameters = method.GetParameters()
                .Where(parameter => parameter.ParameterType != typeof(EvaluationContext))
                .Select(param => new Parameter(param.Name, GetTypeDescriptor(param.ParameterType), param.IsOptional, param.DefaultValue))
                .ToArray();

            if (contextParametersCount > 0)
            {
                return new NativeFunction(
                    method.Name,
                    parameters,
                    (arguments, context) => ConvertResult(method.Invoke(instance, arguments.Prepend(context).ToArray())));
            }
            else
            {
                return new NativeFunction(
                    method.Name,
                    parameters,
                    (arguments, context) => ConvertResult(method.Invoke(instance, arguments)));
            }
        }

        /// <summary>
        /// Returns the MScript type descriptor for the given 'native' type, if the type is supported.
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        public static TypeDescriptor GetTypeDescriptor(Type type)
        {
            if (type == typeof(double) || type == typeof(double?))
                return BaseTypes.Number;
            else if (type == typeof(double[]))
                return BaseTypes.Vector;
            else if (type == typeof(string))
                return BaseTypes.String;
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
        private static object ConvertResult(object result)
        {
            switch (result)
            {
                case true: return 1.0;
                case false: return null;
                default: return result;
            }
        }
    }
}
