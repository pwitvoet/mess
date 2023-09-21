namespace MScript.Parsing.AST
{
    class BinaryOperation : Expression
    {
        public BinaryOperator Operator { get; }
        public Expression LeftOperand { get; }
        public Expression RightOperand { get; }

        public BinaryOperation(BinaryOperator @operator, Expression leftOperand, Expression rightOperand, Position position)
            : base(position)
        {
            Operator = @operator;
            LeftOperand = leftOperand;
            RightOperand = rightOperand;
        }


        public override string ToString() => $"({LeftOperand} {Operator} {RightOperand})";
    }
}
