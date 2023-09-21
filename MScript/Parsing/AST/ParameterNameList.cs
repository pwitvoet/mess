namespace MScript.Parsing.AST
{
    class ParameterNameList
    {
        public List<string> ParameterNames { get; }
        public Position Position { get; }

        public ParameterNameList(Position position)
        {
            ParameterNames = new List<string>();
            Position = position;
        }

        public ParameterNameList(string parameterName, Position position)
        {
            ParameterNames = new List<string> { parameterName };
            Position = position;
        }

        public ParameterNameList(IEnumerable<string> parameterNames, Position position)
        {
            ParameterNames = parameterNames.ToList();
            Position = position;
        }
    }
}
