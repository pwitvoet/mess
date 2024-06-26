﻿namespace MScript.Tokenizing
{
    public enum TokenType
    {
        EndOfInput,         // special end-of-input marker

        Number,             // \d+(\.\d+)?
        String,             // '[^']*'
        Identifier,         // \w[\w\d]*

        SingleEquals,       // =
        ParensOpen,         // (
        ParensClose,        // )
        BracketOpen,        // [
        BracketClose,       // ]
        BraceOpen,          // {
        BraceClose,         // }
        Period,             // .
        Comma,              // ,
        QuestionMark,       // ?
        Colon,              // :
        Semicolon,          // ;
        DoubleAmpersand,    // &&
        DoubleBar,          // ||
        FatArrow,           // =>

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

        ShiftLeft,          // <<
        ShiftRight,         // >>
        SingleAmpersand,    // &
        Caret,              // ^
        SingleBar,          // |
        Tilde,              // ~

        None,               // none
        And,                // and
        Or,                 // or
        Not,                // not
        If,                 // if
        Else,               // else

        Comment,            // //, /* */
    }
}
