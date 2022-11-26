namespace MScript.Parsing.AST
{
    class MemberAccess : Expression
    {
        public Expression Object { get; }
        public string MemberName { get; }

        public MemberAccess(Expression @object, string memberName)
        {
            Object = @object;
            MemberName = memberName;
        }


        public override string ToString() => $"{Object}.{MemberName}";
    }
}
