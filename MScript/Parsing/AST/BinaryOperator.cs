namespace MScript.Parsing.AST
{
    enum BinaryOperator
    {
        // Arithmetic:
        Add,                // exp + exp
        Subtract,           // exp - exp
        Multiply,           // exp * exp
        Divide,             // exp / exp
        Remainder,          // exp % exp

        // Equality:
        Equals,             // exp == exp
        NotEquals,          // exp != exp

        // Comparison:
        GreaterThan,        // exp > exp
        GreaterThanOrEqual, // exp >= exp
        LessThan,           // exp < exp
        LessThanOrEqual,    // exp <= exp

        // Logical:
        And,                // exp && exp
        Or,                 // exp || exp
    }
}
