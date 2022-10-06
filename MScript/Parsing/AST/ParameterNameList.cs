namespace MScript.Parsing.AST
{
    class ParameterNameList
    {
        public List<string> ParameterNames { get; }

        public ParameterNameList(params string[] parameterNames)
        {
            ParameterNames = parameterNames.ToList();
        }
    }
}
