using System;
using System.Collections.Generic;

namespace MESS.Util
{
    public class StringEqualityComparer : IEqualityComparer<string>
    {
        public static StringEqualityComparer InvariantIgnoreCase { get; } = new StringEqualityComparer(
            (s1, s2) => string.Equals(s1, s2, StringComparison.InvariantCultureIgnoreCase),
            s => s.ToLowerInvariant().GetHashCode());


        private Func<string, string, bool> _equals;
        private Func<string, int> _getHashCode;


        public StringEqualityComparer(Func<string, string, bool> equals, Func<string, int> getHashCode)
        {
            _equals = equals;
            _getHashCode = getHashCode;
        }

        public bool Equals(string s1, string s2) => _equals(s1, s2);

        public int GetHashCode(string s) => _getHashCode(s);
    }
}
