using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public class HotspotTexturing
    {
        public static void ApplyHotspotTexturing(Face face, HotspotData hotspotData, bool useAlternate, Random random)
        {
            var availableHotspotRectangles = hotspotData.GetHotspotRectanglesForTexture(face.TextureName);
            if (availableHotspotRectangles is null)
                return;

            availableHotspotRectangles = availableHotspotRectangles
                .Where(hotspotRectangle => hotspotRectangle.IsAlternate == useAlternate)
                .ToArray();
            if (!availableHotspotRectangles.Any())
                return;


            // Find the best texture orientation - with the smallest bounding box, and being as up-right as possible:
            var isFlatFace = GetNearestAxis(face.Plane.Normal) == Axis.Z;
            var possibleProjections = GetPossibleFaceProjections(face).ToArray();
            var bestProjection = possibleProjections  //GetPossibleFaceProjections(face)
                    .OrderBy(projection => projection.BoundingBox.Surface)                                                  // Select the smallest bounding boxes
                    .ThenByDescending(projection => Math.Abs(isFlatFace ? projection.DownAxis.Y : projection.DownAxis.Z))   // Select the projection that best aligns with our up axis
                    .ThenBy(projection => isFlatFace ? projection.DownAxis.Y : projection.DownAxis.Z)                       // Take upright over upside-down
                .First();

            // Rotate the projection by 180 degrees if it's upside down:
            if (isFlatFace ? bestProjection.DownAxis.Y > 0 : bestProjection.DownAxis.Z > 0)
                bestProjection = RotateFaceProjection180Degrees(bestProjection);

            // Order hotspots based on how suitable they are. Generally speaking, the less we need to scale a rect, the better:
            var candidates = availableHotspotRectangles
                .Select(hotspotRectangle => new { hotspotRectangle, score = GetHotspotScore(bestProjection.BoundingBox, hotspotRectangle) })
                .OrderByDescending(candidate => candidate.score)
                .ToArray();

            // Take the best hotspots:
            var bestScore = candidates.First().score;
            var bestHotspots = candidates
                .TakeWhile(candidate => candidate.score == bestScore)
                .Select(candidate => candidate.hotspotRectangle)
                .ToArray();

            // Reject tiling hotspots if we also have non-tiling hotspots:
            if (bestHotspots.Any(hotspot => hotspot.TilingMode != TilingMode.None) && bestHotspots.Any(hotspot => hotspot.TilingMode == TilingMode.None))
                bestHotspots = bestHotspots.Where(hotspot => hotspot.TilingMode == TilingMode.None).ToArray();

            // Randomly select a hotspot from the ones that are left over, and apply it to the given face:
            var selectedHotspot = SelectRandomHotspotRectangle(bestHotspots, random);
            ApplyHotspotRectangleToFace(face, bestProjection, selectedHotspot, random);
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
        private static float GetHotspotScore(Rectangle boundingBox, HotspotRectangle hotspotRectangle)
        {
            var score = GetNonRotatedScore(boundingBox, hotspotRectangle);
            if (hotspotRectangle.AllowRotation)
                score = Math.Max(score, GetNonRotatedScore(new Rectangle(0, 0, boundingBox.Height, boundingBox.Width), hotspotRectangle));
            return score;
        }

        private static float GetNonRotatedScore(Rectangle boundingBox, HotspotRectangle hotspotRectangle)
        {
            // NOTE: Tiling textures should have *some* penalty so that they score less than a perfect fitting rectangle, but not so much of a penalty that they'll be avoided altogether!
            var widthScore = 0.75f;
            if (hotspotRectangle.TilingMode != TilingMode.Horizontal)
            {
                if (boundingBox.Width < hotspotRectangle.Rectangle.Width)
                    widthScore = boundingBox.Width / hotspotRectangle.Rectangle.Width;
                else
                    widthScore = hotspotRectangle.Rectangle.Width / boundingBox.Width;
            }

            var heightScore = 0.75f;
            if (hotspotRectangle.TilingMode != TilingMode.Vertical)
            {
                if (boundingBox.Height < hotspotRectangle.Rectangle.Height)
                    heightScore = boundingBox.Height / hotspotRectangle.Rectangle.Height;
                else
                    heightScore = hotspotRectangle.Rectangle.Height / boundingBox.Height;
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

        private static void ApplyHotspotRectangleToFace(Face face, FaceProjection faceProjection, HotspotRectangle hotspotRectangle, Random random)
        {
            if (hotspotRectangle.AllowRotation)
            {
                var defaultScore = GetNonRotatedScore(faceProjection.BoundingBox, hotspotRectangle);
                var rotatedScore = GetNonRotatedScore(new Rectangle(0, 0, faceProjection.BoundingBox.Height, faceProjection.BoundingBox.Width), hotspotRectangle);
                if (rotatedScore > defaultScore)
                {
                    var isFlatFace = GetNearestAxis(face.Plane.Normal) == Axis.Z;
                    var rotateClockwise = (isFlatFace ? faceProjection.RightAxis.Z : faceProjection.RightAxis.Y) >= 0;
                    faceProjection = RotateFaceProjection90Degrees(faceProjection, rotateClockwise);
                }
            }

            if (hotspotRectangle.AllowMirroring)
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
                scaleX = 1f;
            else if (hotspotRectangle.TilingMode == TilingMode.Vertical)
                scaleY = 1f;

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
