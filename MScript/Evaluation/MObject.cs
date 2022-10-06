namespace MScript.Evaluation
{
    public class MObject : IEquatable<MObject>
    {
        public IReadOnlyDictionary<string, object?> Fields { get; }


        public MObject(IDictionary<string, object?> fields)
        {
            Fields = fields.ToDictionary(kv => kv.Key, kv => kv.Value);
        }


        public override bool Equals(object? obj) => obj is MObject other && Equals(other);

        public override int GetHashCode() => Fields.Count;

        public override string ToString() => $"{{{string.Join(", ", Fields.Select(kv => $"{kv.Key}: {Operations.ToString(kv.Value)}"))}}}";

        public bool Equals(MObject? other)
        {
            if (other is null || Fields.Count != other.Fields.Count)
                return false;

            foreach (var kv in Fields)
                if (!other.Fields.TryGetValue(kv.Key, out var value) || !Operations.IsTrue(Operations.Equals(kv.Value, value)))
                    return false;

            return true;
        }
    }
}
