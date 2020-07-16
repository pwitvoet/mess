using System.Linq;

namespace MScript.Evaluation
{
    class BoundMethod : IFunction
    {
        public int ParameterCount => _function.ParameterCount - 1;


        private object _object;
        private IFunction _function;


        public BoundMethod(object @object, IFunction function)
        {
            _object = @object;
            _function = function;
        }


        public object Apply(object[] arguments, EvaluationContext context)
        {
            return _function.Apply(new[] { _object }.Concat(arguments).ToArray(), context);
        }
    }
}
