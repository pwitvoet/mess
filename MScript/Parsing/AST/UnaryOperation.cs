using System;

namespace MScript.Parsing.AST
{
    class UnaryOperation : Expression
    {
        public UnaryOperator Operator { get; }
        public Expression Operand { get; }

        public UnaryOperation(UnaryOperator @operator, Expression operand)
        {
            Operator = @operator;
            Operand = operand;
        }
    }
}
