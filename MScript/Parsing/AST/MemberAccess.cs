namespace MScript.Parsing.AST
{
    class MemberAccess : Expression
    {
        public Expression Object { get; }
        public string MemberName { get; }

        public MemberAccess(Expression @object, string memberName, Position position)
            : base(position)
        {
            Object = @object;
            MemberName = memberName;
        }


        public override string ToString() => $"{Object}.{MemberName}";
    }
}
