namespace MScript.Parsing.AST
{
    class UnaryOperation : Expression
    {
        public UnaryOperator Operator { get; }
        public Expression Operand { get; }

        public UnaryOperation(UnaryOperator @operator, Expression operand, Position position)
            : base(position)
        {
            Operator = @operator;
            Operand = operand;
        }


        public override string ToString() => $"({Operator} {Operand})";
    }
}
