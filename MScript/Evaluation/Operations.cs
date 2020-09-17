using MScript.Parsing;
using System;
using System.Linq;

namespace MScript.Evaluation
{
    static class Operations
    {
        // Arithmetic:
        public static object Add(object leftOperand, object rightOperand)
        {
            if (leftOperand is null && rightOperand is null)
                return null;

            if (leftOperand is string || rightOperand is string)
                return ToString(leftOperand) + ToString(rightOperand);

            return NumericOperation(leftOperand, rightOperand, (a, b) => a + b);
        }

        public static object Subtract(object leftOperand, object rightOperand) => NumericOperation(leftOperand, rightOperand, (a, b) => a - b);

        public static object Multiply(object leftOperand, object rightOperand) => NumericOperation(leftOperand, rightOperand, (a, b) => a * b);

        public static object Divide(object leftOperand, object rightOperand) => NumericOperation(leftOperand, rightOperand, (a, b) => a / b);

        public static object Remainder(object leftOperand, object rightOperand) => NumericOperation(leftOperand, rightOperand, (a, b) => a % b);


        // Equality:
        public static new object Equals(object leftOperand, object rightOperand)
        {
            if (ReferenceEquals(leftOperand, rightOperand))
                return ToBoolean(true);

            if (leftOperand is null && rightOperand is null)
                return ToBoolean(true);

            if (leftOperand is double leftNumber && rightOperand is double rightNumber)
                return ToBoolean(leftNumber == rightNumber);

            if (leftOperand is double[] leftVector && rightOperand is double[] rightVector)
                return ToBoolean(Enumerable.SequenceEqual(leftVector, rightVector));

            if (leftOperand is string leftString)
                return ToBoolean(leftString == rightOperand?.ToString());

            if (rightOperand is string rightString)
                return ToBoolean(leftOperand?.ToString() == rightString);

            return ToBoolean(false);
        }

        public static object NotEquals(object leftOperand, object rightOperand) => ToBoolean(!IsTrue(Equals(leftOperand, rightOperand)));


        // Comparison:
        public static object GreaterThan(object leftOperand, object rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a > b);

        public static object GreaterThanOrEqual(object leftOperand, object rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a >= b);

        public static object LessThan(object leftOperand, object rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a < b);

        public static object LessThanOrEqual(object leftOperand, object rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a <= b);


        // Logical:
        public static object And(Expression leftOperand, Expression rightOperand, EvaluationContext context)
        {
            if (IsTrue(Evaluator.Evaluate(leftOperand, context)))
            {
                var rightValue = Evaluator.Evaluate(rightOperand, context);
                if (IsTrue(rightValue))
                    return rightValue;
            }

            return ToBoolean(false);
        }

        public static object Or(Expression leftOperand, Expression rightOperand, EvaluationContext context)
        {
            var leftValue = Evaluator.Evaluate(leftOperand, context);
            if (IsTrue(leftValue))
                return leftValue;

            var rightValue = Evaluator.Evaluate(rightOperand, context);
            if (IsTrue(rightValue))
                return rightValue;

            return ToBoolean(false);
        }


        // Negation (unary):
        public static object Negate(object operand)
        {
            switch (operand)
            {
                case null: return null;
                case double number: return -number;
                case double[] vector: return vector.Select(n => -n).ToArray();
            }

            throw new InvalidOperationException($"Cannot negate {operand}.");
        }

        public static object LogicalNegate(object operand) => ToBoolean(!IsTrue(operand));


        // Conditional:
        public static object Conditional(Expression condition, Expression trueExpression, Expression falseExpression, EvaluationContext context)
        {
            if (IsTrue(Evaluator.Evaluate(condition, context)))
                return Evaluator.Evaluate(trueExpression, context);
            else
                return Evaluator.Evaluate(falseExpression, context);
        }

        // Indexing:
        public static double? Index(double[] vector, int index)
        {
            index = GetIndex(vector.Length, index);
            if (index < 0 || index >= vector.Length)
                return null;

            return vector[index];
        }

        public static string Index(string @string, int index)
        {
            index = GetIndex(@string.Length, index);
            if (index < 0 || index >= @string.Length)
                return null;

            return @string[index].ToString();
        }


        // Others:
        public static bool IsTrue(object value)
        {
            switch (value)
            {
                default:
                case null: return false;
                case double number: return number != 0.0;
                case double[] vector: return vector.Length != 0;
                case string @string: return @string.Length != 0;
            }
        }

        public static string ToString(object value)
        {
            if (value is double[] vector)
                return string.Join(" ", vector);

            return value?.ToString() ?? "";
        }


        private static object NumericOperation(object leftOperand, object rightOperand, Func<double, double, double> operation)
        {
            if (leftOperand is double[] leftVector)
            {
                if (rightOperand is double[] rightVector)
                    return VectorOperation(leftVector, rightVector, operation);
                else if (rightOperand is double rightNumber)
                    return VectorOperation(leftVector, Enumerable.Range(0, leftVector.Length).Select(i => rightNumber).ToArray(), operation);
                else if (rightOperand is null)
                    return VectorOperation(leftVector, Enumerable.Repeat(0.0, leftVector.Length).ToArray(), operation);
            }
            else if (leftOperand is double leftNumber)
            {
                if (rightOperand is double[] rightVector)
                    return VectorOperation(Enumerable.Range(0, rightVector.Length).Select(i => leftNumber).ToArray(), rightVector, operation);
                else if (rightOperand is double rightNumber)
                    return operation(leftNumber, rightNumber);
                else if (rightOperand is null)
                    return operation(leftNumber, 0.0);
            }
            else if (leftOperand is null)
            {
                if (rightOperand is double[] rightVector)
                    return VectorOperation(Enumerable.Repeat(0.0, rightVector.Length).ToArray(), rightVector, operation);
                else if (rightOperand is double rightNumber)
                    return operation(0.0, rightNumber);
                else if (rightOperand is null)
                    return null;
            }

            throw new InvalidOperationException($"Cannot perform numeric operation on {leftOperand} and {rightOperand}.");
        }

        private static double[] VectorOperation(double[] leftVector, double[] rightVector, Func<double, double, double> operation)
        {
            var length = Math.Max(leftVector.Length, rightVector.Length);
            var left = leftVector.Concat(Enumerable.Repeat(0.0, length - leftVector.Length));
            var right = rightVector.Concat(Enumerable.Repeat(0.0, length - rightVector.Length));
            return left.Zip(right, operation).ToArray();
        }

        private static object NumericComparison(object leftOperand, object rightOperand, Func<double, double, bool> comparison)
        {
            if (leftOperand is double leftNumber)
            {
                if (rightOperand is double rightNumber)
                    return ToBoolean(comparison(leftNumber, rightNumber));
                else if (rightOperand is null)
                    return ToBoolean(comparison(leftNumber, 0.0));
            }
            else if (leftOperand is null)
            {
                if (rightOperand is double rightNumber)
                    return ToBoolean(comparison(0.0, rightNumber));
                else if (rightOperand is null)
                    return ToBoolean(comparison(0.0, 0.0));
            }

            throw new InvalidOperationException($"Cannot compare {leftOperand} and {rightOperand}.");
        }

        private static int GetIndex(int length, int index) => index < 0 ? length + index : index;

        private static object ToBoolean(bool boolean) => boolean ? 1.0 : 0.0;
    }
}
