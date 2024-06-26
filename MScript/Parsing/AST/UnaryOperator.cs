﻿namespace MScript.Parsing.AST
{
    enum UnaryOperator
    {
        // Negation:
        Negate,             // -exp
        LogicalNegate,      // !exp

        // Bitwise:
        BitwiseComplement,  // ~exp
    }
}
