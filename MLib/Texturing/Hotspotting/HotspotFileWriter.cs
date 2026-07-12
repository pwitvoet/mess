using MLib.Mathematics.Spatial;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MLib.Texturing.Hotspotting
{
    public static class HotspotFileWriter
    {
        public static void Save(string filePath, HotspotFileData hotspotFileData)
        {
            using (var file = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                Save(file, hotspotFileData);
        }

        public static void Save(Stream file, HotspotFileData hotspotFileData)
        {
            var hotspotsNode = new JsonObject();
            foreach (var rectangleSet in hotspotFileData.RectangleSets)
                hotspotsNode[rectangleSet.Name] = ToJson(rectangleSet);

            var texturesNode = new JsonObject();
            foreach (var binding in hotspotFileData.Bindings)
                texturesNode[binding.TextureNamePattern] = ToJson(binding);

            var root = new JsonObject();
            root["hotspots"] = hotspotsNode;
            root["textures"] = texturesNode;
            JsonSerializer.Serialize(file, root);
        }


        /// <summary>
        /// Serializes the given array of hotspots to a JSON array.
        /// </summary>
        public static string Serialize(IEnumerable<HotspotRectangle> hotspotRectangles)
        {
            var array = new JsonArray();
            foreach (var hotspotRectangle in hotspotRectangles)
                array.Add(ToJson(hotspotRectangle));
            return array.ToJsonString();
        }


        private static JsonObject ToJson(HotspotRectangleSet hotspotRectangleSet)
        {
            var rectanglesArray = new JsonArray();
            foreach (var rectangle in hotspotRectangleSet.Rectangles)
                rectanglesArray.Add(ToJson(rectangle));

            var json = new JsonObject();
            json["rectangles"] = rectanglesArray;
            return json;
        }

        private static JsonObject ToJson(HotspotBinding hotspotBinding)
        {
            var json = new JsonObject();
            json["hotspot"] = hotspotBinding.HotspotName;

            if (hotspotBinding.FallbackTextureNamePattern != null)
                json["fallback_texture"] = hotspotBinding.FallbackTextureNamePattern;

            if (hotspotBinding.FallbackScoreThreshold != 0)
                json["fallback_score_threshold"] = hotspotBinding.FallbackScoreThreshold;

            if (hotspotBinding.Labels.Any())
                json["labels"] = ToJsonArray(hotspotBinding.Labels);

            return json;
        }


        private static JsonObject ToJson(HotspotRectangle hotspotRectangle)
        {
            var json = new JsonObject();

            json["rectangle"] = ToJson(hotspotRectangle.Rectangle);

            if (hotspotRectangle.AllowRotation)
                json["allow_rotation"] = hotspotRectangle.AllowRotation;

            if (hotspotRectangle.AllowedMirroring != Mirrorings.None)
                json["allow_mirroring"] = ToString(hotspotRectangle.AllowedMirroring);

            if (hotspotRectangle.HorizontalLayout != HotspotLayout.Fit)
                json["horizontal_layout"] = ToString(hotspotRectangle.HorizontalLayout);

            if (hotspotRectangle.VerticalLayout != HotspotLayout.Fit)
                json["vertical_layout"] = ToString(hotspotRectangle.VerticalLayout);

            if (hotspotRectangle.SnapWidth != null)
                json["snap_width"] = hotspotRectangle.SnapWidth;

            if (hotspotRectangle.SnapHeight != null)
                json["snap_height"] = hotspotRectangle.SnapHeight;

            if (hotspotRectangle.SelectionWeight != 1)
                json["selection_weight"] = hotspotRectangle.SelectionWeight;

            if (hotspotRectangle.ConcaveEdges != ConcaveEdges.None)
                json["concave_edges"] = ToJsonArray(hotspotRectangle.ConcaveEdges);

            if (hotspotRectangle.Labels.Any())
                json["labels"] = ToJsonArray(hotspotRectangle.Labels);

            return json;
        }

        private static JsonObject ToJson(Rectangle rectangle)
        {
            var json = new JsonObject();

            json["x"] = rectangle.X;
            json["y"] = rectangle.Y;
            json["width"] = rectangle.Width;
            json["height"] = rectangle.Height;

            return json;
        }

        private static JsonArray ToJsonArray(IEnumerable<string> labels)
        {
            var array = new JsonArray();

            foreach (var label in labels)
                array.Add(label);

            return array;
        }

        private static JsonArray ToJsonArray(ConcaveEdges concaveEdges)
        {
            var array = new JsonArray();

            if (concaveEdges.HasFlag(ConcaveEdges.Top))     array.Add("top");
            if (concaveEdges.HasFlag(ConcaveEdges.Right))   array.Add("right");
            if (concaveEdges.HasFlag(ConcaveEdges.Bottom))  array.Add("bottom");
            if (concaveEdges.HasFlag(ConcaveEdges.Left))    array.Add("left");

            return array;
        }

        private static string ToString(Mirrorings mirrorings) => mirrorings switch {
            Mirrorings.Horizontal => "horizontal",
            Mirrorings.Vertical => "vertical",
            Mirrorings.Horizontal | Mirrorings.Vertical => "both",
            _ => "",
        };

        private static string ToString(HotspotLayout hotspotLayout) => hotspotLayout switch {
            HotspotLayout.Fit => "fit",
            HotspotLayout.Clip => "clip",
            HotspotLayout.Tile => "tile",
            _ => "",
        };
    }
}
