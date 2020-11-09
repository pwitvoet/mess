using MScript.Evaluation.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MScript.Evaluation
{
    public class NativeFunction : IFunction
    {
        public string Name { get; }
        public IReadOnlyList<Parameter> Parameters { get; }


        private Func<object[], EvaluationContext, object> _func;


        public NativeFunction(string name, IEnumerable<Parameter> parameters, Func<object[], EvaluationContext, object> func)
        {
            Name = name;
            Parameters = parameters?.ToArray() ?? Array.Empty<Parameter>();
            _func = func;
        }

        public object Apply(object[] arguments, EvaluationContext context)
        {
            if (arguments == null || arguments.Length != Parameters.Count)
                throw new InvalidOperationException($"Invalid parameter count: expected {Parameters.Count} but got {arguments?.Length}.");

            for (int i = 0; i < arguments.Length; i++)
            {
                var argumentType = TypeDescriptor.GetType(arguments[i]);
                if (argumentType == BaseTypes.Any)
                    continue;

                if (argumentType != Parameters[i].Type && !(Parameters[i].IsOptional && argumentType == BaseTypes.None))
                    throw new InvalidOperationException($"Parameter '{Parameters[i].Name}' is of type {Parameters[i].Type.Name}, but a {argumentType.Name} was given.");
            }

            return _func(arguments, context);
        }
    }
}
