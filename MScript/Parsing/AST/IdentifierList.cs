namespace MScript.Parsing.AST
{
    class IdentifierList
    {
        public List<string> Identifiers { get; }

        public IdentifierList(params string[] identifiers)
        {
            Identifiers = identifiers.ToList();
        }
    }
}
