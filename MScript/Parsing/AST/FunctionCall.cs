using System;
using System.Collections.Generic;
using System.Linq;

namespace MScript.Parsing.AST
{
    class FunctionCall : Expression
    {
        public Expression Function { get; }
        public IReadOnlyCollection<Expression> Arguments { get; }

        public FunctionCall(Expression function, IEnumerable<Expression> arguments)
        {
            Function = function;
            Arguments = arguments.ToArray();
        }
    }
}
