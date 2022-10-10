using MScript.Parsing;

namespace MScript.Evaluation
{
    public class AnonymousFunction : IFunction
    {
        public string Name => "";
        public IReadOnlyList<Parameter> Parameters { get; }


        private Expression _body;
        private EvaluationContext _capturedContext;


        public AnonymousFunction(IEnumerable<string> argumentNames, Expression body, EvaluationContext capturedContext)
        {
            Parameters = argumentNames
                .Select(name => new Parameter(name, BaseTypes.Any))
                .ToArray();

            _body = body;
            _capturedContext = capturedContext;
        }

        public override string ToString() => $"<FUNCTION ({string.Join(", ", Parameters.Select(parameter => parameter.Name))})>";


        public object? Apply(object?[] arguments)
        {
            var localContext = new EvaluationContext(parentContext: _capturedContext);
            for (int i = 0; i < Parameters.Count; i++)
                localContext.Bind(Parameters[i].Name, i < arguments.Length ? arguments[i] : Parameters[i].DefaultValue);

            return Evaluator.Evaluate(_body, localContext);
        }
    }
}
