namespace MScript.Parsing.AST
{
    class ObjectLiteral : Literal
    {
        public List<(string, Expression)> Fields { get; }


        public ObjectLiteral(IEnumerable<(string, Expression)> fields)
        {
            Fields = fields.ToList();
        }
    }
}
