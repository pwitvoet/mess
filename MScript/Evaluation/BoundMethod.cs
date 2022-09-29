namespace MScript.Evaluation
{
    class BoundMethod : IFunction
    {
        public string Name => _function.Name;
        public IReadOnlyList<Parameter> Parameters => _function.Parameters.Skip(1).ToArray();


        private object? _object;
        private IFunction _function;


        public BoundMethod(object? @object, IFunction function)
        {
            _object = @object;
            _function = function;
        }

        public override string ToString() => $"<FUNCTION {Name}>";


        public object? Apply(object?[] arguments, EvaluationContext context) => _function.Apply(new[] { _object }.Concat(arguments).ToArray(), context);
    }
}
