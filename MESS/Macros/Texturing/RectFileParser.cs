using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public static class RectFileParser
    {
        /// <summary>
        /// Parses a .rect file and returns an array of hotspot rectangles.
        /// This uses the format shown here: https://developer.valvesoftware.com/wiki/Hotspot_texturing ,
        /// with a few extra properties for tiling and selection weight.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        public static HotspotRectangle[] Parse(string input)
        {
            var hotspotRectangles = new List<HotspotRectangle>();
            var context = new Context(Tokenize(input));
            while (!context.IsExhausted)
            {
                Expect(context, "Rectangles");
                Expect(context, "{");

                while (context.Current == "rectangle")
                {
                    context.MoveNext();
                    Expect(context, "{");

                    var min = new Vector2D();
                    var max = new Vector2D();
                    var allowMirroring = false;
                    var allowRotation = false;
                    var isAlternate = false;
                    var tilingMode = TilingMode.None;
                    var selectionWeight = 1f;

                    while (context.Current != "}")
                    {
                        var key = context.Current;
                        context.MoveNext();

                        switch (key)
                        {
                            case "min": min = ReadVector2D(context); break;
                            case "max": max = ReadVector2D(context); break;
                            case "reflect": allowMirroring = ReadBool(context); break;
                            case "rotate": allowRotation = ReadBool(context); break;
                            case "alt": isAlternate = ReadBool(context); break;
                            case "tiling": tilingMode = ReadTilingMode(context); break;
                            case "weight": selectionWeight = ReadFloat(context); break;

                            default:
                                throw new InvalidDataException($"Unknown rectangle key: '{key}'.");
                        }
                    }

                    Expect(context, "}");

                    var hotspotRectangle = new HotspotRectangle(new Rectangle(min.X, min.Y, max.X - min.X, max.Y - min.Y), allowMirroring, allowRotation, isAlternate, tilingMode, selectionWeight);
                    hotspotRectangles.Add(hotspotRectangle);
                }

                Expect(context, "}");
            }
            return hotspotRectangles.ToArray();
        }


        private static IEnumerable<string> Tokenize(string input)
        {
            var start = 0;
            var stringLiteral = false;
            var comment = false;

            for (int i = 0; i < input.Length; i++)
            {
                if (comment)
                {
                    if (input[i] == '\r' || input[i] == '\n')
                    {
                        comment = false;
                        start = i + 1;
                    }
                }
                else if (stringLiteral)
                {
                    if (input[i] == '"')
                    {
                        yield return input.Substring(start, i - start + 1);

                        stringLiteral = false;
                        start = i + 1;
                    }
                }
                else
                {
                    if (input[i] == '/' && i > 0 && input[i - 1] == '/')
                    {
                        comment = true;
                        start = i - 1;
                    }
                    else if (input[i] == '"')
                    {
                        if (i > start)
                            yield return input.Substring(start, i - start);

                        stringLiteral = true;
                        start = i;
                    }
                    else if (char.IsWhiteSpace(input[i]))
                    {
                        if (i > start)
                            yield return input.Substring(start, i - start);

                        start = i + 1;
                    }
                }
            }

            if (start < input.Length)
                yield return input.Substring(start);
        }

        private static void Expect(Context context, string token)
        {
            if (context.IsExhausted)
                throw new InvalidDataException($"Expected '{token}' but found end of input!");

            if (context.Current != token)
                throw new InvalidDataException($"Expected '{token}' but found '{context.Current}'!");

            context.MoveNext();
        }

        private static Vector2D ReadVector2D(Context context)
            => ReadValue(context, s => (TryParseVector2D(s, out var value), value), "2D vector");

        private static bool ReadBool(Context context)
            => ReadValue(context, s => (s == "0" || s == "1", s == "1"), "boolean");

        private static float ReadFloat(Context context)
            => ReadValue(context, s => (float.TryParse(s, out var value), value), "number");

        private static TilingMode ReadTilingMode(Context context)
            => ReadValue(context, s => (Enum.TryParse<TilingMode>(s, true, out var value), value), "tiling mode");

        private static TValue ReadValue<TValue>(Context context, Func<string, (bool, TValue)> parse, string typeName)
        {
            if (context.IsExhausted)
                throw new InvalidDataException($"Expected a {typeName} but found end of input!");

            (var success, var value) = parse(context.Current.Trim('"'));
            if (!success)
                throw new InvalidDataException($"Expected a {typeName} but found '{context.Current}'!");

            context.MoveNext();
            return value;
        }

        private static bool TryParseVector2D(string str, out Vector2D result)
        {
            var parts = str.Split();
            if (parts.Length == 2 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
            {
                result = new Vector2D(x, y);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }


        class Context : IDisposable
        {
            public string Current { get; private set; }
            public bool IsExhausted { get; private set; }

            private IEnumerator<string> _tokens;


            public Context(IEnumerable<string> tokens)
            {
                _tokens = tokens.GetEnumerator();
                Current = "";
                MoveNext();
            }

            public void Dispose()
            {
                _tokens.Dispose();
            }

            public bool MoveNext()
            {
                if (_tokens.MoveNext())
                {
                    Current = _tokens.Current;
                    return true;
                }
                else
                {
                    IsExhausted = true;
                    return false;
                }
            }
        }
    }
}
