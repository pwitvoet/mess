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
        LogicalAnd,         // exp && exp
        LogicalOr,          // exp || exp

        // Bitwise:
        BitshiftLeft,       // exp << exp
        BitshiftRight,      // exp >> exp
        BitwiseAnd,         // exp & exp
        BitwiseXor,         // exp ^ exp
        BitwiseOr,          // exp | exp
    }
}
