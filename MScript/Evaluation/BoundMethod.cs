using System.Collections.Generic;
using System.Linq;

namespace MScript.Evaluation
{
    class BoundMethod : IFunction
    {
        public IReadOnlyList<Parameter> Parameters => _function.Parameters.Skip(1).ToArray();


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
