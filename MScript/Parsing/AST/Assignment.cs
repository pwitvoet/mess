namespace MScript.Parsing.AST
{
    public class Assignment
    {
        public string Identifier { get; }
        public Expression Value { get; }

        public Assignment(string identifier, Expression value)
        {
            Identifier = identifier;
            Value = value;
        }

        public override string ToString() => $"{Identifier} = {Value}";
    }
}
