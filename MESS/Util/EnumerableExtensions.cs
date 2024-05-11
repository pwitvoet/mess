namespace MESS.Util
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns a collection that can safely be enumerated multiple times.
        /// </summary>
        public static IEnumerable<T> GetSafeEnumerable<T>(this IEnumerable<T> enumerable)
        {
            switch (enumerable)
            {
                case IReadOnlyCollection<T> readOnlyCollection: return readOnlyCollection;
                default: return enumerable.ToArray();
            }
        }
    }
}
