namespace MScript
{
    public class EvaluationException : MScriptException
    {
        public EvaluationException(string message, Position position)
            : base(message, position)
        {
        }

        public EvaluationException(string message, Exception innerException, Position position)
            : base(message, innerException, position)
        {
        }
    }
}
