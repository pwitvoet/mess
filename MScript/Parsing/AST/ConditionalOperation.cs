﻿namespace MScript.Parsing.AST
{
    class ConditionalOperation : Expression
    {
        public Expression Condition { get; }
        public Expression TrueExpression { get; }
        public Expression FalseExpression { get; }

        public ConditionalOperation(Expression condition, Expression trueExpression, Expression falseExpression, Position position)
            : base(position)
        {
            Condition = condition;
            TrueExpression = trueExpression;
            FalseExpression = falseExpression;
        }


        public override string ToString() => $"({Condition} ? {TrueExpression} : {FalseExpression})";
    }
}
