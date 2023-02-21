using MScript.Parsing;
using System.Globalization;

namespace MScript.Evaluation
{
    static class Operations
    {
        // Arithmetic:
        public static object? Add(object? leftOperand, object? rightOperand)
        {
            return RecursiveOperation(leftOperand, rightOperand, (left, right) =>
                {
                    if (left is null && right is null)
                        return null;

                    if (left is string || right is string)
                        return ToString(left) + ToString(right);

                    return RecursiveNumericOperation(left, right, (a, b) => a + b);
                });
        }

        public static object? Subtract(object? leftOperand, object? rightOperand) => RecursiveNumericOperation(leftOperand, rightOperand, (a, b) => a - b);

        public static object? Multiply(object? leftOperand, object? rightOperand) => RecursiveNumericOperation(leftOperand, rightOperand, (a, b) => a * b);

        public static object? Divide(object? leftOperand, object? rightOperand) => RecursiveNumericOperation(leftOperand, rightOperand, (a, b) => a / b);

        public static object? Remainder(object? leftOperand, object? rightOperand) => RecursiveNumericOperation(leftOperand, rightOperand, (a, b) => a % b);


        // Equality:
        public static new object? Equals(object? leftOperand, object? rightOperand)
        {
            if (ReferenceEquals(leftOperand, rightOperand))
                return ToBoolean(true);

            if (leftOperand is null && rightOperand is null)
                return ToBoolean(true);
            else if (leftOperand is null || rightOperand is null)
                return ToBoolean(false);

            if (leftOperand is double leftNumber && rightOperand is double rightNumber)
                return ToBoolean(leftNumber == rightNumber);

            if (leftOperand is object?[] leftArray && rightOperand is object?[] rightArray)
            {
                if (leftArray.Length != rightArray.Length)
                    return false;

                for (int i = 0; i < leftArray.Length; i++)
                    if (!IsTrue(Equals(leftArray[i], rightArray[i])))
                        return false;

                return true;
            }

            if (leftOperand is MObject leftObject && rightOperand is MObject rightObject)
                return ToBoolean(leftObject.Equals(rightObject));

            if (leftOperand is string leftString && rightOperand is string rightString)
                return ToBoolean(leftString == rightString);

            return ToBoolean(false);
        }

        public static object? NotEquals(object? leftOperand, object? rightOperand) => ToBoolean(!IsTrue(Equals(leftOperand, rightOperand)));


        // Comparison:
        public static object? GreaterThan(object? leftOperand, object? rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a > b);

        public static object? GreaterThanOrEqual(object? leftOperand, object? rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a >= b);

        public static object? LessThan(object? leftOperand, object? rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a < b);

        public static object? LessThanOrEqual(object? leftOperand, object? rightOperand) => NumericComparison(leftOperand, rightOperand, (a, b) => a <= b);


        // Logical:
        public static object? And(Expression leftOperand, Expression rightOperand, EvaluationContext context)
        {
            if (IsTrue(Evaluator.Evaluate(leftOperand, context)))
            {
                var rightValue = Evaluator.Evaluate(rightOperand, context);
                if (IsTrue(rightValue))
                    return rightValue;
            }

            return ToBoolean(false);
        }

        public static object? Or(Expression leftOperand, Expression rightOperand, EvaluationContext context)
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
        public static object? Negate(object? operand) => operand switch
        {
            null => null,
            double number => -number,
            object?[] array => array.Select(Negate).ToArray(),

            _ => throw new InvalidOperationException($"Cannot negate {operand}."),
        };

        public static object? LogicalNegate(object? operand) => ToBoolean(!IsTrue(operand));


        // Conditional:
        public static object? Conditional(Expression condition, Expression trueExpression, Expression falseExpression, EvaluationContext context)
        {
            if (IsTrue(Evaluator.Evaluate(condition, context)))
                return Evaluator.Evaluate(trueExpression, context);
            else
                return Evaluator.Evaluate(falseExpression, context);
        }

        // Indexing:
        public static object? Index(object?[] array, int index)
        {
            index = GetIndex(array.Length, index);
            if (index < 0 || index >= array.Length)
                return null;

            return array[index];
        }

        public static string? Index(string @string, int index)
        {
            index = GetIndex(@string.Length, index);
            if (index < 0 || index >= @string.Length)
                return null;

            return @string[index].ToString();
        }


        // Others:
        public static bool IsTrue(object? value) => value is not null;

        public static string ToString(object? value) => value switch
        {
            null => "",
            double number => number.ToString(CultureInfo.InvariantCulture),
            object?[] array => string.Join(" ", array.Select(ToString)),
            _ => value.ToString() ?? "",
        };


        private static object? RecursiveOperation(object? leftOperand, object? rightOperand, Func<object?, object?, object?> operation)
        {
            var leftArray = leftOperand as object?[];
            var rightArray = rightOperand as object?[];

            if (leftArray is not null || rightArray is not null)
            {
                var leftArrayLength = leftArray?.Length ?? 0;
                var rightArrayLength = rightArray?.Length ?? 0;
                var maxLength = Math.Max(leftArrayLength, rightArrayLength);

                var leftDefaultValue = leftArray is null ? leftOperand : null;
                var rightDefaultValue = rightArray is null ? rightOperand : null;

                var result = new object?[maxLength];
                for (int i = 0; i < maxLength; i++)
                {
                    var left = i < leftArrayLength ? leftArray![i] : leftDefaultValue;
                    var right = i < rightArrayLength ? rightArray![i] : rightDefaultValue;
                    result[i] = RecursiveOperation(left, right, operation);
                }
                return result;
            }
            else
            {
                return operation(leftOperand, rightOperand);
            }
        }

        private static object? RecursiveNumericOperation(object? leftOperand, object? rightOperand, Func<double, double, double> operation)
        {
            return RecursiveOperation(leftOperand, rightOperand, (left, right) =>
                {
                    if (GetNumericalValue(left) is not double leftNumber || GetNumericalValue(right) is not double rightNumber)
                        throw new InvalidOperationException($"Cannot perform numeric operation on {left} and {right}.");

                    return operation(leftNumber, rightNumber);
                });
        }

        private static object? NumericComparison(object? leftOperand, object? rightOperand, Func<double, double, bool> comparison)
        {
            if (GetNumericalValue(leftOperand) is not double leftNumber || GetNumericalValue(rightOperand) is not double rightNumber)
                throw new InvalidOperationException($"Cannot compare {leftOperand} and {rightOperand}.");

            return ToBoolean(comparison(leftNumber, rightNumber));
        }

        private static double? GetNumericalValue(object? operand) => operand switch {
            double value => value,
            null => 0.0,
            _ => null,
        };

        private static int GetIndex(int length, int index) => index < 0 ? length + index : index;

        private static object? ToBoolean(bool boolean) => boolean ? 1.0 : null;
    }
}
