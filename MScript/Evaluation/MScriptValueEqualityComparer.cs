using System.Diagnostics.CodeAnalysis;

namespace MScript.Evaluation
{
    internal class MScriptValueEqualityComparer : IEqualityComparer<object?>
    {
        public static MScriptValueEqualityComparer Instance { get; } = new MScriptValueEqualityComparer();


        public new bool Equals(object? left, object? right) => Operations.IsTrue(Operations.Equals(left, right));

        public int GetHashCode([DisallowNull] object? obj) => obj?.GetHashCode() ?? 0;
    }
}
