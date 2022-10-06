namespace MScript.Parsing.AST
{
    class KeyValuePairList
    {
        public List<(string, Expression)> KeyValuePairs { get; }

        public KeyValuePairList(params (string, Expression)[] keyValuePairs)
        {
            KeyValuePairs = keyValuePairs.ToList();
        }
    }
}
