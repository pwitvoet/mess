using MScript.Evaluation.Types;
using MScript.Parsing;
using MScript.Parsing.AST;

namespace MScript.Evaluation
{
    public static class Evaluator
    {
        public static object? Evaluate(Expression expression, EvaluationContext context)
        {
            return expression switch
            {
                NoneLiteral _ => null,
                NumberLiteral numberLiteral => numberLiteral.Value,
                StringLiteral stringLiteral => stringLiteral.Value,
                VectorLiteral vectorLiteral => EvaluateVectorLiteral(vectorLiteral, context),
                ObjectLiteral objectLiteral => EvaluateObjectLiteral(objectLiteral, context),
                AnonymousFunctionDefinition anonymousFunctionDefinition => EvaluateAnonymousFunctionDefinition(anonymousFunctionDefinition, context),
                Variable variable => context.Resolve(variable.Name),
                FunctionCall functionCall => EvaluateFunctionCall(functionCall, context),
                Indexing indexing => EvaluateIndexing(indexing, context),
                MemberAccess memberAccess => EvaluateMemberAccess(memberAccess, context),
                BinaryOperation binaryOperation => EvaluateBinaryOperation(binaryOperation, context),
                UnaryOperation unaryOperation => EvaluateUnaryOperation(unaryOperation, context),
                ConditionalOperation conditionalOperation => EvaluateConditionalOperation(conditionalOperation, context),

                _ => throw new InvalidOperationException($"Unknown expression type: {expression}."),
            };
        }


        private static object EvaluateVectorLiteral(VectorLiteral vectorLiteral, EvaluationContext context)
        {
            var vector = new double[vectorLiteral.Elements.Count];
            for (int i = 0; i < vectorLiteral.Elements.Count; i++)
            {
                if (!((Evaluate(vectorLiteral.Elements[i], context) ?? 0.0) is double number))
                    throw new InvalidOperationException($"A {BaseTypes.Vector} can only contain numbers.");

                vector[i] = number;
            }
            return vector;
        }

        private static object EvaluateObjectLiteral(ObjectLiteral objectLiteral, EvaluationContext context)
        {
            var fields = new Dictionary<string, object?>();
            foreach ((var name, var expression) in objectLiteral.Fields)
                fields[name] = Evaluate(expression, context);

            return new MObject(fields);
        }

        private static object EvaluateAnonymousFunctionDefinition(AnonymousFunctionDefinition anonymousFunctionDefinition, EvaluationContext context)
        {
            return new AnonymousFunction(anonymousFunctionDefinition.ArgumentNames, anonymousFunctionDefinition.Body, context);
        }

        private static object? EvaluateFunctionCall(FunctionCall functionCall, EvaluationContext context)
        {
            var functionResult = Evaluate(functionCall.Function, context);
            if (!(functionResult is IFunction function))
                throw new InvalidOperationException($"A function call requires a {BaseTypes.Function}, not a {TypeDescriptor.GetType(functionResult)}.");

            var arguments = functionCall.Arguments
                .Select(argument => Evaluate(argument, context))
                .ToArray();
            if (arguments.Length < function.Parameters.Count)
            {
                if (!function.Parameters[arguments.Length].IsOptional)
                    throw new InvalidOperationException($"The function '{function.Name}' requires at least {function.Parameters.TakeWhile(parameter => !parameter.IsOptional).Count()} arguments, but only {arguments.Length} were provided.");

                arguments = arguments
                    .Concat(function.Parameters
                        .Skip(arguments.Length)
                        .Select(parameter => parameter.DefaultValue))
                    .ToArray();
            }

            return function.Apply(arguments);
        }

        private static object? EvaluateIndexing(Indexing indexing, EvaluationContext context)
        {
            var indexable = Evaluate(indexing.Indexable, context);
            if (indexable is double[] || indexable is string)
            {
                var indexResult = Evaluate(indexing.Index, context) ?? 0.0;
                if (!(indexResult is double index))
                    throw new InvalidOperationException($"An index must be a {BaseTypes.Number}, not a {TypeDescriptor.GetType(indexResult)}.");

                if (indexable is double[] vector)
                    return Operations.Index(vector, (int)index);
                else if (indexable is string @string)
                    return Operations.Index(@string, (int)index);
            }

            throw new InvalidOperationException($"A {TypeDescriptor.GetType(indexable)} cannot be indexed.");
        }

        private static object? EvaluateMemberAccess(MemberAccess memberAccess, EvaluationContext context)
        {
            var owner = Evaluate(memberAccess.Object, context);
            var type = TypeDescriptor.GetType(owner);
            if (owner is MObject obj)
            {
                if (obj.Fields.TryGetValue(memberAccess.MemberName, out var value))
                    return value;
            }
            else
            {
                var member = type.GetMember(memberAccess.MemberName);
                if (member is not null)
                    return member.GetValue(owner);
            }

            throw new InvalidOperationException($"{type} does not have a member named '{memberAccess.MemberName}'.");
        }

        private static object? EvaluateBinaryOperation(BinaryOperation binaryOperation, EvaluationContext context)
        {
            switch (binaryOperation.Operator)
            {
                case BinaryOperator.And: return Operations.And(binaryOperation.LeftOperand, binaryOperation.RightOperand, context);
                case BinaryOperator.Or: return Operations.Or(binaryOperation.LeftOperand, binaryOperation.RightOperand, context);
            }

            var leftOperand = Evaluate(binaryOperation.LeftOperand, context);
            var rightOperand = Evaluate(binaryOperation.RightOperand, context);
            switch (binaryOperation.Operator)
            {
                case BinaryOperator.Add: return Operations.Add(leftOperand, rightOperand);
                case BinaryOperator.Subtract: return Operations.Subtract(leftOperand, rightOperand);
                case BinaryOperator.Multiply: return Operations.Multiply(leftOperand, rightOperand);
                case BinaryOperator.Divide: return Operations.Divide(leftOperand, rightOperand);
                case BinaryOperator.Remainder: return Operations.Remainder(leftOperand, rightOperand);
                case BinaryOperator.Equals: return Operations.Equals(leftOperand, rightOperand);
                case BinaryOperator.NotEquals: return Operations.NotEquals(leftOperand, rightOperand);
                case BinaryOperator.GreaterThan: return Operations.GreaterThan(leftOperand, rightOperand);
                case BinaryOperator.GreaterThanOrEqual: return Operations.GreaterThanOrEqual(leftOperand, rightOperand);
                case BinaryOperator.LessThan: return Operations.LessThan(leftOperand, rightOperand);
                case BinaryOperator.LessThanOrEqual: return Operations.LessThanOrEqual(leftOperand, rightOperand);
                default: throw new InvalidOperationException($"Unknown operator: {binaryOperation.Operator}.");
            }
        }

        private static object? EvaluateUnaryOperation(UnaryOperation unaryOperation, EvaluationContext context)
        {
            var operand = Evaluate(unaryOperation.Operand, context);
            switch (unaryOperation.Operator)
            {
                case UnaryOperator.Negate: return Operations.Negate(operand);
                case UnaryOperator.LogicalNegate: return Operations.LogicalNegate(operand);
                default: throw new InvalidOperationException($"Unknown operator: {unaryOperation.Operator}.");
            }
        }

        private static object? EvaluateConditionalOperation(ConditionalOperation conditionalOperation, EvaluationContext context)
        {
            return Operations.Conditional(
                conditionalOperation.Condition,
                conditionalOperation.TrueExpression,
                conditionalOperation.FalseExpression,
                context);
        }
    }
}
