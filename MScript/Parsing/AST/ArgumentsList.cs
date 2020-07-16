using System.Collections.Generic;
using System.Linq;

namespace MScript.Parsing.AST
{
    class ArgumentsList
    {
        public List<Expression> Arguments { get; }

        public ArgumentsList(params Expression[] arguments)
        {
            Arguments = arguments?.ToList() ?? new List<Expression>();
        }
    }
}
