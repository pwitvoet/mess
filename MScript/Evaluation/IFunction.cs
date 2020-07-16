
namespace MScript.Evaluation
{
    interface IFunction
    {
        int ParameterCount { get; }


        object Apply(object[] arguments, EvaluationContext context);
    }
}
