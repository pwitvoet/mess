using System;

namespace MScript.Parsing.AST
{
    class BinaryOperation : Expression
    {
        public BinaryOperator Operator { get; }
        public Expression LeftOperand { get; }
        public Expression RightOperand { get; }

        public BinaryOperation(BinaryOperator @operator, Expression leftOperand, Expression rightOperand)
        {
            Operator = @operator;
            LeftOperand = leftOperand;
            RightOperand = rightOperand;
        }
    }
}
