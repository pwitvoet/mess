namespace MScript
{
    public class Position
    {
        public int Line { get; }
        public int Offset { get; }

        public Position(int line, int offset)
        {
            Line = line;
            Offset = offset;
        }

        public override string ToString() => $"(Line: {Line}, Offset: {Offset})";
    }
}
