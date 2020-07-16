using System;

namespace MScript.Evaluation
{
    class NativeFunction : IFunction
    {
        public int ParameterCount { get; }


        private Func<object[], EvaluationContext, object> _func;


        public NativeFunction(int parameterCount, Func<object[], EvaluationContext, object> func)
        {
            ParameterCount = parameterCount;
            _func = func;
        }

        public object Apply(object[] arguments, EvaluationContext context)
        {
            if (arguments == null || arguments.Length != ParameterCount)
                throw new InvalidOperationException($"Invalid parameter count: expected {ParameterCount} but got {arguments?.Length}.");

            return _func(arguments, context);
        }
    }
}
