using MScript.Evaluation.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MScript.Evaluation
{
    public class NativeFunction : IFunction
    {
        public IReadOnlyList<Parameter> Parameters { get; }


        private Func<object[], EvaluationContext, object> _func;


        public NativeFunction(IEnumerable<Parameter> parameters, Func<object[], EvaluationContext, object> func)
        {
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
                if (argumentType != Parameters[i].Type)
                    throw new InvalidOperationException($"Parameter '{Parameters[i].Name}' is of type {Parameters[i].Type.Name}, but a {argumentType.Name} was given.");
            }

            return _func(arguments, context);
        }
    }
}
