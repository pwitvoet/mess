namespace MScript.Parsing.AST
{
    class ObjectLiteral : Literal
    {
        public List<(string, Expression)> Fields { get; }


        public ObjectLiteral(IEnumerable<(string, Expression)> fields)
        {
            Fields = fields.ToList();
        }


        public override string ToString() => $"{{{string.Join(", ", Fields.Select(field => $"{field.Item1}: {field.Item2}"))}}}";
    }
}
