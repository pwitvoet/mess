using MESS.Mathematics.Spatial;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MESS.Macros.Texturing
{
    public static class HotspotFileParser
    {
        /// <summary>
        /// Parses a .hotspot file, storing the results into the given hotspot data collection.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        /// <exception cref="JsonException"></exception>
        public static IDictionary<string, HotspotData> Parse(Stream file)
        {
            var result = new Dictionary<string, HotspotData>();
            var json = JsonSerializer.Deserialize<JsonObject>(file);
            if (json == null)
                throw new InvalidDataException("Hotspot file must not be empty.");


            var hotspotRectangles = new Dictionary<string, HotspotRectangle[]>();
            var hotspotsNode = json["hotspots"];
            if (hotspotsNode != null)
            {
                foreach (var node in hotspotsNode.AsObject())
                {
                    if (node.Value == null)
                        continue;

                    var hotspotName = node.Key;
                    hotspotRectangles[hotspotName] = ParseHotspotRectangles(node.Value.AsObject());
                }
            }

            var texturesNode = json["textures"];
            if (texturesNode != null)
            {
                foreach (var node in texturesNode.AsObject())
                {
                    if (node.Value == null)
                        continue;

                    var textureName = node.Key;
                    var hotspotName = node.Value["hotspot"]?.ToString();
                    if (hotspotName == null)
                        throw new InvalidDataException("Texture must contain a 'hotspot' key.");

                    if (!hotspotRectangles.TryGetValue(hotspotName, out var rectangles))
                        throw new InvalidDataException($"Texture '{textureName}' is referencing non-existing hotspot data '{hotspotName}'.");

                    var fallbackTexture = node.Value["fallback_texture"]?.ToString();
                    var fallbackScoreThreshold = (double?)node.Value["fallback_score_threshold"]?.AsValue();

                    var labels = ParseStringArray(node.Value["labels"]?.AsArray());

                    result[node.Key] = new HotspotData(rectangles, fallbackTexture, fallbackScoreThreshold ?? 0, labels);
                }
            }

            return result;
        }


        private static HotspotRectangle[] ParseHotspotRectangles(JsonObject json)
        {
            var rectanglesNode = json["rectangles"];
            if (rectanglesNode == null)
                throw new InvalidDataException("Hotspot object must contain a 'rectangles' key.");

            var hotspotRectangles = new List<HotspotRectangle>();
            foreach (var node in rectanglesNode.AsArray())
            {
                if (node == null)
                    continue;

                var jsonObject = node.AsObject();
                var rectangleNode = jsonObject["rectangle"];
                if (rectangleNode == null)
                    throw new InvalidDataException("Hotspot rectangle must contain a 'rectangle' key.");

                var rectangle = ParseRectangle(rectangleNode.AsObject());
                var allowRotation = (bool?)jsonObject["allow_rotation"]?.AsValue() ?? false;
                var allowedMirroring = ParseMirrorings(jsonObject["allow_mirroring"]?.ToString());
                var tilingMode = ParseTilingMode(jsonObject["tiling_mode"]?.ToString());
                var selectionWeight = (double?)jsonObject["selection_weight"]?.AsValue() ?? 1;
                var concaveEdges = ParseConcaveEdges(jsonObject["concave_edges"]?.AsArray());
                var labels = ParseStringArray(jsonObject["labels"]?.AsArray());

                var hotspotRectangle = new HotspotRectangle(rectangle, allowRotation, allowedMirroring, tilingMode, selectionWeight, concaveEdges, labels);
                hotspotRectangles.Add(hotspotRectangle);
            }
            return hotspotRectangles.ToArray();
        }

        private static Rectangle ParseRectangle(JsonObject json)
        {
            return new Rectangle(
                (double)(json["x"]?.AsValue() ?? throw new InvalidDataException("Rectangle must contain an 'x' key.")),
                (double)(json["y"]?.AsValue() ?? throw new InvalidDataException("Rectangle must contain an 'y' key.")),
                (double)(json["width"]?.AsValue() ?? throw new InvalidDataException("Rectangle must contain a 'width' key.")),
                (double)(json["height"]?.AsValue() ?? throw new InvalidDataException("Rectangle must contain a 'height' key.")));
        }

        private static Mirrorings ParseMirrorings(string? str)
        {
            switch (str?.ToLowerInvariant())
            {
                case "horizontal": return Mirrorings.Horizontal;
                case "vertical": return Mirrorings.Vertical;
                case "both": return Mirrorings.Horizontal | Mirrorings.Vertical;
                default: return Mirrorings.None;
            }
        }

        private static TilingMode ParseTilingMode(string? str)
        {
            switch (str?.ToLowerInvariant())
            {
                case "horizontal": return TilingMode.Horizontal;
                case "vertical": return TilingMode.Vertical;
                default: return TilingMode.None;
            }
        }

        private static ConcaveEdges ParseConcaveEdges(JsonArray? array)
        {
            if (array == null)
                return ConcaveEdges.None;

            var result = ConcaveEdges.None;
            foreach (var part in array)
            {
                switch (part?.AsValue().ToString().ToLowerInvariant())
                {
                    case "top": result |= ConcaveEdges.Top; break;
                    case "right": result |= ConcaveEdges.Right; break;
                    case "bottom": result |= ConcaveEdges.Bottom; break;
                    case "left": result |= ConcaveEdges.Left; break;
                    default: throw new InvalidDataException($"Invalid edge constraint: '{part}'.");
                }
            }
            return result;
        }

        private static string[]? ParseStringArray(JsonArray? array)
        {
            if (array == null)
                return null;

            return array
                .Where(part => part != null)
                .Select(part => part!.AsValue().ToString())
                .ToArray();
        }
    }
}
