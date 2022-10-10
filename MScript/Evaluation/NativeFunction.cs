using MScript.Evaluation.Types;

namespace MScript.Evaluation
{
    public class NativeFunction : IFunction
    {
        public string Name { get; }
        public IReadOnlyList<Parameter> Parameters { get; }


        private Func<object?[], object?> _func;


        public NativeFunction(string name, IEnumerable<Parameter> parameters, Func<object?[], object?> func)
        {
            Name = name;
            Parameters = parameters?.ToArray() ?? Array.Empty<Parameter>();
            _func = func;
        }

        public override string ToString() => $"<FUNCTION {Name}>";


        public object? Apply(object?[] arguments)
        {
            if (arguments == null || arguments.Length != Parameters.Count)
                throw new InvalidOperationException($"Invalid parameter count: expected {Parameters.Count} but got {arguments?.Length}.");

            for (int i = 0; i < arguments.Length; i++)
            {
                var parameter = Parameters[i];
                var argumentType = TypeDescriptor.GetType(arguments[i]);
                if (parameter.Type == BaseTypes.Any || argumentType == parameter.Type)
                    continue;

                if (argumentType != BaseTypes.None)
                    throw new InvalidOperationException($"Parameter '{parameter.Name}' is of type {parameter.Type.Name}, but a {argumentType.Name} was given.");

                // 'None' is valid for optional parameters. For non-optional parameters,
                // we'll convert it to a default value instead (if possible):
                if (!parameter.IsOptional)
                {
                    if (parameter.Type == BaseTypes.Number)
                        arguments[i] = 0.0;
                    else if (parameter.Type == BaseTypes.Vector)
                        arguments[i] = new double[] { };
                    else if (parameter.Type == BaseTypes.String)
                        arguments[i] = "";
                    else
                        throw new InvalidOperationException($"Parameter '{parameter.Name}' is of type {parameter.Type.Name}. {argumentType.Name} cannot be converted to this type.");
                }
            }

            return _func(arguments);
        }
    }
}
