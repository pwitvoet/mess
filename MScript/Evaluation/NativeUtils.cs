using MScript.Evaluation.Types;
using System;
using System.Linq;
using System.Reflection;

namespace MScript.Evaluation
{
    public static class NativeUtils
    {
        /// <summary>
        /// Creates an MScript function that calls a 'native' method.
        /// The method parameters must all be MScript-compatible types.
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        public static NativeFunction CreateFunction(MethodInfo method)
        {
            // TODO: param.DefaultValue may not be an 'MScript-compatible' type!
            var parameters = method.GetParameters()
                .Select(param => new Parameter(param.Name, GetTypeDescriptor(param.ParameterType), param.IsOptional, param.DefaultValue))
                .ToArray();

            return new NativeFunction(method.Name, parameters, (arguments, context) =>
            {
                var result = method.Invoke(null, arguments);
                switch (result)
                {
                    case true: return 1.0;
                    case false: return null;
                    default: return result;
                }
            });
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

            throw new NotSupportedException($"No type descriptor for {type.FullName}.");
        }
    }
}
