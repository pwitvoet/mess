
namespace MScript.Tokenizing
{
    enum TokenType
    {
        EndOfInput,         // special end-of-input marker

        Number,             // \d+(\.\d+)?
        String,             // '[^']*'
        Identifier,         // \w[\w\d]*

        ParensOpen,         // (
        ParensClose,        // )
        BracketOpen,        // [
        BracketClose,       // ]
        Period,             // .
        Comma,              // ,
        QuestionMark,       // ?
        Colon,              // :

        Plus,               // +
        Minus,              // -
        Asterisk,           // *
        Slash,              // /
        PercentageSign,     // %
        ExclamationMark,    // !
        Equals,             // ==
        NotEquals,          // !=
        GreaterThan,        // >
        GreaterThanOrEqual, // >=
        LessThan,           // <
        LessThanOrEqual,    // <=

        None,               // none
        And,                // and
        Or,                 // or
        If,                 // if
        Else,               // else
    }
}
