using System.Collections.Generic;

namespace MScript.Evaluation
{
    public interface IFunction
    {
        IReadOnlyList<Parameter> Parameters { get; }


        object Apply(object[] arguments, EvaluationContext context);
    }
}
