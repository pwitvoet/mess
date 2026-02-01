using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public class HotspotSettings
    {
        /// <summary>
        /// The default texture scale. Hotspotting will try to stay as close as possible to this scale.
        /// </summary>
        public float DefaultTextureScale { get; set; } = 1f;

        /// <summary>
        /// When false, rectangles will not be rotated, even if they are marked with 'enable rotation'.
        /// </summary>
        public bool AllowRotation { get; set; } = true;

        /// <summary>
        /// When false, rectangles will not be (randomly) mirrored, even if they are marked with 'enable mirroring'.
        /// </summary>
        public bool AllowMirroring { get; set; } = true;

        /// <summary>
        /// When true, only rectangles that are marked as 'alternate' will be used. Otherwise only non-alternate rectangles are used.
        /// This can be used to divide textures into two groups, such as two different kinds of materials.
        /// </summary>
        public bool UseAlternateRectangles { get; set; } = false;

        /// <summary>
        /// When false, tiling rectangles will not be used.
        /// </summary>
        public bool AllowTilingRectangles { get; set; } = true;

        /// <summary>
        /// When true, non-tiling rectangles will always be chosen instead of tiling rectangles
        /// if they are an equally good match.
        /// </summary>
        public bool PreferNonTilingRectangles { get; set; } = true;

        /// <summary>
        /// When false, tiling rectangles will only be scaled along their non-tiling side, to fit the faces they are applied to.
        /// Otherwise, they will be scaled equally along their tiling side as well.
        /// </summary>
        public bool UniformScalingForTilingRectangles { get; set; } = true;
    }


    public class HotspotTexturing
    {
        public static void ApplyHotspotTexturing(Face face, HotspotData hotspotData, HotspotSettings settings, Random random)
        {
            var availableHotspotRectangles = hotspotData.GetHotspotRectanglesForTexture(face.TextureName);
            if (availableHotspotRectangles is null)
                return;

            availableHotspotRectangles = availableHotspotRectangles
                .Where(hotspotRectangle => hotspotRectangle.IsAlternate == settings.UseAlternateRectangles)
                .Where(hotspotRectangle => hotspotRectangle.TilingMode == TilingMode.None || settings.AllowTilingRectangles)
                .ToArray();
            if (!availableHotspotRectangles.Any())
                return;


            // Find the best texture orientation - with the smallest bounding box, and being as up-right as possible:
            var isFlatFace = GetNearestAxis(face.Plane.Normal) == Axis.Z;
            var possibleProjections = GetPossibleFaceProjections(face).ToArray();
            var smallestSurface = possibleProjections.Min(projection => projection.BoundingBox.Surface);

            var bestProjection = possibleProjections
                .Where(projection => projection.BoundingBox.Surface < smallestSurface * 1.01)                           // Select the projections with the smallest bounding boxes (with some wiggle room to account for numeric precision issues)
                .OrderByDescending(projection => Math.Abs(isFlatFace ? projection.DownAxis.Y : projection.DownAxis.Z))  // Select the projection that best aligns with our up axis
                .ThenBy(projection => isFlatFace ? projection.DownAxis.Y : projection.DownAxis.Z)                       // Upright is better than upside-down
                .First();

            // Rotate the projection by 180 degrees if it's upside down:
            if (isFlatFace ? bestProjection.DownAxis.Y > 0 : bestProjection.DownAxis.Z > 0)
                bestProjection = RotateFaceProjection180Degrees(bestProjection);

            // Order hotspots based on how suitable they are. Generally speaking, the less we need to scale a rect, the better:
            var candidates = availableHotspotRectangles
                .Select(hotspotRectangle => new { hotspotRectangle, score = GetHotspotScore(bestProjection.BoundingBox, hotspotRectangle, settings) })
                .OrderByDescending(candidate => candidate.score)
                .ToArray();

            // Take the best hotspots:
            var bestScore = candidates.First().score;
            var bestHotspots = candidates
                .TakeWhile(candidate => candidate.score > bestScore * 0.99f)
                .Select(candidate => candidate.hotspotRectangle)
                .ToArray();

            var bestHotspotsOld = bestHotspots.ToArray();
            if (settings.PreferNonTilingRectangles)
            {
                // Reject tiling hotspots if we also have non-tiling hotspots:
                if (bestHotspots.Any(hotspot => hotspot.TilingMode != TilingMode.None) && bestHotspots.Any(hotspot => hotspot.TilingMode == TilingMode.None))
                    bestHotspots = bestHotspots.Where(hotspot => hotspot.TilingMode == TilingMode.None).ToArray();
            }

            // Randomly select a hotspot from the ones that are left over, and apply it to the given face:
            var selectedHotspot = SelectRandomHotspotRectangle(bestHotspots, random);
            ApplyHotspotRectangleToFace(face, bestProjection, selectedHotspot, settings, random);
        }

        public enum Axis
        {
            X,
            Y,
            Z,
        }

        private static Axis GetNearestAxis(Vector3D normal)
        {
            var absX = Math.Abs(normal.X);
            var absY = Math.Abs(normal.Y);
            var absZ = Math.Abs(normal.Z);

            if (absZ > absX && absZ > absY)
                return Axis.Z;
            else if (absY > absX)
                return Axis.Y;
            else
                return Axis.X;
        }

        // Returns a face projection (right/down axis + bounding box) for each edge, using that edge as horizontal axis.
        private static IEnumerable<FaceProjection> GetPossibleFaceProjections(Face face)
        {
            for (int i = 0, prevI = face.Vertices.Count - 1; i < face.Vertices.Count; prevI = i, i++)
            {
                var rightAxis = (face.Vertices[i] - face.Vertices[prevI]).Normalized();
                var downAxis = rightAxis.CrossProduct(face.Plane.Normal);

                var minX = float.MaxValue;
                var minY = float.MaxValue;
                var maxX = float.MinValue;
                var maxY = float.MinValue;
                foreach (var vertex in face.Vertices)
                {
                    var x = vertex.DotProduct(rightAxis);
                    var y = vertex.DotProduct(downAxis);

                    minX = Math.Min(x, minX);
                    minY = Math.Min(y, minY);
                    maxX = Math.Max(x, maxX);
                    maxY = Math.Max(y, maxY);
                }
                var boundingBox = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                yield return new FaceProjection(rightAxis, downAxis, boundingBox);
            }
        }

        private static FaceProjection FlipFaceProjection(FaceProjection faceProjection, bool horizontal, bool vertical)
        {
            return new FaceProjection(
                horizontal ? -faceProjection.RightAxis : faceProjection.RightAxis,
                vertical ? -faceProjection.DownAxis : faceProjection.DownAxis,
                new Rectangle(
                    horizontal ? -(faceProjection.BoundingBox.X + faceProjection.BoundingBox.Width) : faceProjection.BoundingBox.X,
                    vertical ? -(faceProjection.BoundingBox.Y + faceProjection.BoundingBox.Height) : faceProjection.BoundingBox.Y,
                    faceProjection.BoundingBox.Width,
                    faceProjection.BoundingBox.Height));
        }

        private static FaceProjection RotateFaceProjection90Degrees(FaceProjection faceProjection, bool clockwise)
        {
            if (clockwise)
            {
                return new FaceProjection(
                    faceProjection.DownAxis,
                    -faceProjection.RightAxis,
                    new Rectangle(
                        -faceProjection.BoundingBox.Y,
                        faceProjection.BoundingBox.X,
                        faceProjection.BoundingBox.Height,
                        faceProjection.BoundingBox.Width));
            }
            else
            {
                return new FaceProjection(
                    -faceProjection.DownAxis,
                    faceProjection.RightAxis,
                    new Rectangle(
                        faceProjection.BoundingBox.Y,
                        -faceProjection.BoundingBox.X,
                        faceProjection.BoundingBox.Height,
                        faceProjection.BoundingBox.Width));
            }
        }

        private static FaceProjection RotateFaceProjection180Degrees(FaceProjection faceProjection)
            => FlipFaceProjection(faceProjection, true, true);

        // Returns a score that indicates how good of a match the given hotspot rectangle is for the given bounding box.
        private static float GetHotspotScore(Rectangle boundingBox, HotspotRectangle hotspotRectangle, HotspotSettings settings)
        {
            var score = GetNonRotatedScore(boundingBox, hotspotRectangle, settings);
            if (settings.AllowRotation && hotspotRectangle.AllowRotation)
                score = Math.Max(score, GetNonRotatedScore(new Rectangle(0, 0, boundingBox.Height, boundingBox.Width), hotspotRectangle, settings));
            return score;
        }

        private static float GetNonRotatedScore(Rectangle boundingBox, HotspotRectangle hotspotRectangle, HotspotSettings settings)
        {
            // NOTE: Tiling textures should have *some* penalty so that they score less than a perfect fitting rectangle, but not so much of a penalty that they'll be avoided altogether!
            var widthScore = 0.75f;
            if (hotspotRectangle.TilingMode != TilingMode.Horizontal)
            {
                var scaledRectangleWidth = hotspotRectangle.Rectangle.Width * settings.DefaultTextureScale;
                if (boundingBox.Width < scaledRectangleWidth)
                    widthScore = boundingBox.Width / scaledRectangleWidth;
                else
                    widthScore = scaledRectangleWidth / boundingBox.Width;
            }

            var heightScore = 0.75f;
            if (hotspotRectangle.TilingMode != TilingMode.Vertical)
            {
                var scaledRectangleHeight = hotspotRectangle.Rectangle.Height * settings.DefaultTextureScale;
                if (boundingBox.Height < scaledRectangleHeight)
                    heightScore = boundingBox.Height / scaledRectangleHeight;
                else
                    heightScore = scaledRectangleHeight / boundingBox.Height;
            }

            if (settings.UniformScalingForTilingRectangles)
            {
                if (hotspotRectangle.TilingMode == TilingMode.Horizontal)
                    widthScore = heightScore;
                else if (hotspotRectangle.TilingMode == TilingMode.Vertical)
                    heightScore = widthScore;
            }

            // NOTE: Squaring each side's score should favor square pixel over stretched ones?
            return (widthScore * widthScore) * (heightScore * heightScore);
        }

        private static HotspotRectangle SelectRandomHotspotRectangle(HotspotRectangle[] hotspotRectangles, Random random)
        {
            var totalWeight = hotspotRectangles.Sum(hotspot => hotspot.SelectionWeight);
            var selection = (float)(random.NextDouble() * totalWeight);
            foreach (var hotspotRectangle in hotspotRectangles)
            {
                selection -= hotspotRectangle.SelectionWeight;
                if (selection <= 0)
                    return hotspotRectangle;
            }
            return hotspotRectangles.Last();
        }

        private static void ApplyHotspotRectangleToFace(Face face, FaceProjection faceProjection, HotspotRectangle hotspotRectangle, HotspotSettings settings, Random random)
        {
            if (settings.AllowRotation && hotspotRectangle.AllowRotation)
            {
                var defaultScore = GetNonRotatedScore(faceProjection.BoundingBox, hotspotRectangle, settings);
                var rotatedScore = GetNonRotatedScore(new Rectangle(0, 0, faceProjection.BoundingBox.Height, faceProjection.BoundingBox.Width), hotspotRectangle, settings);
                if (rotatedScore > defaultScore)
                {
                    var isFlatFace = GetNearestAxis(face.Plane.Normal) == Axis.Z;
                    var rotateClockwise = (isFlatFace ? faceProjection.RightAxis.Z : faceProjection.RightAxis.Y) >= 0;
                    faceProjection = RotateFaceProjection90Degrees(faceProjection, rotateClockwise);
                }
            }

            if (settings.AllowMirroring && hotspotRectangle.AllowMirroring)
            {
                faceProjection = FlipFaceProjection(
                    faceProjection,
                    horizontal: random.Next(2) == 1,
                    vertical: random.Next(2) == 1);
            }


            face.TextureRightAxis = faceProjection.RightAxis;
            face.TextureDownAxis = faceProjection.DownAxis;

            var scaleX = faceProjection.BoundingBox.Width / hotspotRectangle.Rectangle.Width;
            var scaleY = faceProjection.BoundingBox.Height / hotspotRectangle.Rectangle.Height;

            if (hotspotRectangle.TilingMode == TilingMode.Horizontal)
                scaleX = settings.UniformScalingForTilingRectangles ? scaleY : 1f;
            else if (hotspotRectangle.TilingMode == TilingMode.Vertical)
                scaleY = settings.UniformScalingForTilingRectangles ? scaleX : 1f;

            face.TextureScale = new Vector2D(scaleX, scaleY);
            face.TextureShift = new Vector2D(
                (-faceProjection.BoundingBox.X / scaleX) + hotspotRectangle.Rectangle.X,
                (-faceProjection.BoundingBox.Y / scaleY) + hotspotRectangle.Rectangle.Y);
            face.TextureAngle = 0;
        }


        class FaceProjection
        {
            public Vector3D RightAxis { get; }
            public Vector3D DownAxis { get; }
            public Rectangle BoundingBox { get; }

            public FaceProjection(Vector3D rightAxis, Vector3D downAxis, Rectangle boundingBox)
            {
                RightAxis = rightAxis;
                DownAxis = downAxis;
                BoundingBox = boundingBox;
            }
        }
    }
}
