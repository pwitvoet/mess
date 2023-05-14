namespace MScript.Evaluation
{
    public class MObject : IEquatable<MObject>
    {
        public IReadOnlyDictionary<string, object?> Fields { get; }


        public MObject(IEnumerable<KeyValuePair<string, object?>> fields)
        {
            var fieldsDictionary = new Dictionary<string, object?>();
            foreach (var field in fields)
                fieldsDictionary[field.Key] = field.Value;

            Fields = fieldsDictionary;
        }

        public MObject CreateCopy()
            => new MObject(Fields);

        public MObject CreateCopyWithField(string fieldName, object? value)
            => new MObject(Fields.Append(KeyValuePair.Create(fieldName, value)));

        public MObject CreateCopyWithFields(IEnumerable<KeyValuePair<string, object?>> fields)
            => new MObject(Fields.Concat(fields));

        public MObject CreateCopyWithoutField(string fieldName)
            => new MObject(Fields.Where(field => field.Key != fieldName));

        public MObject CreateCopyWithoutFields(IEnumerable<string> fieldNames)
        {
            var fieldNamesSet = fieldNames.ToHashSet();
            return new MObject(Fields.Where(field => !fieldNamesSet.Contains(field.Key)));
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
