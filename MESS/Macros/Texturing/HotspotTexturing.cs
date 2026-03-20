using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public class HotspotSettings
    {
        /// <summary>
        /// The default texture scale. Hotspotting will try to stay as close as possible to this scale.
        /// </summary>
        public double DefaultTextureScale { get; set; } = 1;

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

        /// <summary>
        /// These textures will be ignored by the hotspotting algorithm.
        /// Any edge that borders a face with one of these textures will be considered a concave edge,
        /// which affects rectangle selection.
        /// </summary>
        public HashSet<string> IgnoreTextures { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    }


    public class HotspotTexturing
    {
        /// <summary>
        /// Applies hotspot texturing to the given face.
        /// Returns the score of the applied hotspot rectangle.
        /// </summary>
        public static double ApplyHotspotTexturing(Face face, Brush parentBrush, HotspotData hotspotData, HotspotSettings settings, Random random)
        {
            var availableHotspotRectangles = hotspotData.HotspotRectangles
                .Where(hotspotRectangle => hotspotRectangle.IsAlternate == settings.UseAlternateRectangles)
                .Where(hotspotRectangle => hotspotRectangle.TilingMode == TilingMode.None || settings.AllowTilingRectangles)
                .ToArray();
            if (!availableHotspotRectangles.Any())
                return 0;


            var bestProjection = GetBestProjection(face);

            // Rotate the projection by 180 degrees if it's upside down:
            var isFlatFace = GetNearestAxis(face.Plane.Normal) == Axis.Z;
            if (isFlatFace ? bestProjection.DownAxis.Y > 0 : bestProjection.DownAxis.Z > 0)
                bestProjection = RotateFaceProjection180Degrees(bestProjection);

            var edgeConstraints = GetEdgeConstraints(face, parentBrush, bestProjection, settings);

            // Order hotspots based on how suitable they are. Generally speaking, the less we need to scale a rect, the better:
            var candidates = availableHotspotRectangles
                .Select(hotspotRectangle => GetBestHotspotScore(bestProjection.BoundingBox, edgeConstraints, hotspotRectangle, settings))
                .OrderByDescending(candidate => candidate.Score)
                .ToArray();

            // Take the best hotspots (with some wiggle room to account for numerical precision issues):
            var bestScore = candidates.First();
            var bestHotspotScores = candidates
                .TakeWhile(candidate => candidate.Score > bestScore.Score * 0.99f)
                .ToArray();

            if (settings.PreferNonTilingRectangles)
            {
                // Reject tiling hotspots if we also have non-tiling hotspots:
                if (bestHotspotScores.Any(candidate => candidate.HotspotRectangle.TilingMode != TilingMode.None) && bestHotspotScores.Any(candidate => candidate.HotspotRectangle.TilingMode == TilingMode.None))
                    bestHotspotScores = bestHotspotScores.Where(hotspot => hotspot.HotspotRectangle.TilingMode == TilingMode.None).ToArray();
            }

            // Randomly select a hotspot from the ones that are left over, and apply it to the given face:
            var selectedHotspotScore = SelectRandomHotspotRectangleScore(bestHotspotScores, random);
            ApplyHotspotRectangleToFace(face, bestProjection, selectedHotspotScore, settings, random);

            return selectedHotspotScore.Score;
        }

        /// <summary>
        /// Returns the most suitable texture projection axis for the given face.
        /// </summary>
        public static (Vector3D rightAxis, Vector3D downAxis) GetBestTextureAxis(Face face)
        {
            var bestProjection = GetBestProjection(face);
            return (bestProjection.RightAxis, bestProjection.DownAxis);
        }

        // Finds the best texture orientation - the projection with the smallest bounding box, being as up-right as possible.
        private static FaceProjection GetBestProjection(Face face)
        {
            // Find the best texture orientation - with the smallest bounding box, and being as up-right as possible:
            var isFlatFace = GetNearestAxis(face.Plane.Normal) == Axis.Z;
            var possibleProjections = GetPossibleFaceProjections(face).ToArray();
            var smallestSurface = possibleProjections.Min(projection => projection.BoundingBox.Surface);

            return possibleProjections
                .Where(projection => projection.BoundingBox.Surface < smallestSurface * 1.01)                           // Select the projections with the smallest bounding boxes (with some wiggle room to account for numeric precision issues)
                .OrderByDescending(projection => Math.Abs(isFlatFace ? projection.DownAxis.Y : projection.DownAxis.Z))  // Select the projection that best aligns with our up axis
                .ThenBy(projection => isFlatFace ? projection.DownAxis.Y : projection.DownAxis.Z)                       // Upright is better than upside-down
                .First();
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

                var minX = double.MaxValue;
                var minY = double.MaxValue;
                var maxX = double.MinValue;
                var maxY = double.MinValue;
                var edgeVertices = new int[4];     // Top, right, down, left.

                var projectedVertices = face.Vertices
                    .Select(vertex => new Vector2D(vertex.DotProduct(rightAxis), vertex.DotProduct(downAxis)))
                    .ToArray();
                for (int j = 0; j < projectedVertices.Length; j++)
                {
                    var x = projectedVertices[j].X;
                    var y = projectedVertices[j].Y;

                    // Keep track of the most extreme vertices in order to find aligned edges later:
                    if (x < minX) edgeVertices[FaceProjection.Left] = j;
                    if (x > maxX) edgeVertices[FaceProjection.Right] = j;
                    if (y < minY) edgeVertices[FaceProjection.Top] = j;
                    if (y > maxY) edgeVertices[FaceProjection.Bottom] = j;

                    minX = Math.Min(x, minX);
                    minY = Math.Min(y, minY);
                    maxX = Math.Max(x, maxX);
                    maxY = Math.Max(y, maxY);
                }

                var boundingBox = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                var alignedEdges = FindAlignedEdges(projectedVertices, boundingBox, edgeVertices);
                yield return new FaceProjection(rightAxis, downAxis, boundingBox, alignedEdges);
            }

            int[] FindAlignedEdges(Vector2D[] projectedVertices, Rectangle boundingBox, int[] edgeVertices, double threshold = 0.5)
            {
                return new int[] {
                    GetAlignedEdge(projectedVertices, edgeVertices[FaceProjection.Top], v => v.Y, threshold),
                    GetAlignedEdge(projectedVertices, edgeVertices[FaceProjection.Right], v => v.X, threshold),
                    GetAlignedEdge(projectedVertices, edgeVertices[FaceProjection.Bottom], v => v.Y, threshold),
                    GetAlignedEdge(projectedVertices, edgeVertices[FaceProjection.Left], v => v.X, threshold),
                };
            }

            int GetAlignedEdge(Vector2D[] projectedVertices, int index, Func<Vector2D, double> getValue, double threshold)
            {
                var edgeValue = getValue(projectedVertices[index]);

                var prevIndex = GetPreviousVertexIndex(face, index);
                if (Math.Abs(edgeValue - getValue(projectedVertices[prevIndex])) < threshold)
                    return prevIndex;

                var nextIndex = GetNextVertexIndex(face, index);
                if (Math.Abs(edgeValue - getValue(projectedVertices[nextIndex])) < threshold)
                    return index;

                return FaceProjection.NoEdge;
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
                    faceProjection.BoundingBox.Height),
                new int[] {
                    faceProjection.AlignedEdges[vertical   ? FaceProjection.Bottom : FaceProjection.Top],
                    faceProjection.AlignedEdges[horizontal ? FaceProjection.Left : FaceProjection.Right],
                    faceProjection.AlignedEdges[vertical   ? FaceProjection.Top : FaceProjection.Bottom],
                    faceProjection.AlignedEdges[horizontal ? FaceProjection.Right : FaceProjection.Left],
                });
        }

        private static FaceProjection RotateFaceProjection90Degrees(FaceProjection faceProjection, bool clockwise)
        {
            if (clockwise)
            {
                return new FaceProjection(
                    faceProjection.DownAxis,
                    -faceProjection.RightAxis,
                    new Rectangle(
                        faceProjection.BoundingBox.Y,
                        -faceProjection.BoundingBox.X - faceProjection.BoundingBox.Width,
                        faceProjection.BoundingBox.Height,
                        faceProjection.BoundingBox.Width),
                    faceProjection.AlignedEdges.Skip(1).Append(faceProjection.AlignedEdges[0]).ToArray());
            }
            else
            {
                return new FaceProjection(
                    -faceProjection.DownAxis,
                    faceProjection.RightAxis,
                    new Rectangle(
                        -faceProjection.BoundingBox.Y - faceProjection.BoundingBox.Height,
                        faceProjection.BoundingBox.X,
                        faceProjection.BoundingBox.Height,
                        faceProjection.BoundingBox.Width),
                    faceProjection.AlignedEdges.Take(3).Prepend(faceProjection.AlignedEdges[3]).ToArray());
            }
        }

        private static FaceProjection RotateFaceProjection180Degrees(FaceProjection faceProjection)
            => FlipFaceProjection(faceProjection, true, true);

        private static HotspotRectangleScore SelectRandomHotspotRectangleScore(HotspotRectangleScore[] hotspotRectangleScores, Random random)
        {
            var totalWeight = hotspotRectangleScores.Sum(hotspot => hotspot.HotspotRectangle.SelectionWeight);
            var selection = random.NextDouble() * totalWeight;
            foreach (var hotspotRectangleScore in hotspotRectangleScores)
            {
                selection -= hotspotRectangleScore.HotspotRectangle.SelectionWeight;
                if (selection <= 0)
                    return hotspotRectangleScore;
            }
            return hotspotRectangleScores.Last();
        }

        private static void ApplyHotspotRectangleToFace(Face face, FaceProjection faceProjection, HotspotRectangleScore hotspotRectangleScore, HotspotSettings settings, Random random)
        {
            var orientation = hotspotRectangleScore.Orientations[random.Next(hotspotRectangleScore.Orientations.Length)];

            switch (orientation & HotspotOrientations.RotationMask)
            {
                case HotspotOrientations.Rotate90Degrees: faceProjection = RotateFaceProjection90Degrees(faceProjection, false); break;
                case HotspotOrientations.Rotate180Degrees: faceProjection = RotateFaceProjection180Degrees(faceProjection); break;
                case HotspotOrientations.Rotate270Degrees: faceProjection = RotateFaceProjection90Degrees(faceProjection, true); break;
            }

            switch (orientation & HotspotOrientations.MirrorMask)
            {
                case HotspotOrientations.MirrorHorizontally: faceProjection = FlipFaceProjection(faceProjection, true, false); break;
                case HotspotOrientations.MirrorVertically: faceProjection = FlipFaceProjection(faceProjection, false, true); break;
            }


            face.TextureRightAxis = faceProjection.RightAxis;
            face.TextureDownAxis = faceProjection.DownAxis;

            var scaleX = faceProjection.BoundingBox.Width / hotspotRectangleScore.HotspotRectangle.Rectangle.Width;
            var scaleY = faceProjection.BoundingBox.Height / hotspotRectangleScore.HotspotRectangle.Rectangle.Height;

            if (hotspotRectangleScore.HotspotRectangle.TilingMode == TilingMode.Horizontal)
                scaleX = settings.UniformScalingForTilingRectangles ? scaleY : 1;
            else if (hotspotRectangleScore.HotspotRectangle.TilingMode == TilingMode.Vertical)
                scaleY = settings.UniformScalingForTilingRectangles ? scaleX : 1;

            face.TextureScale = new Vector2D(scaleX, scaleY);
            face.TextureShift = new Vector2D(
                (-faceProjection.BoundingBox.X / scaleX) + hotspotRectangleScore.HotspotRectangle.Rectangle.X,
                (-faceProjection.BoundingBox.Y / scaleY) + hotspotRectangleScore.HotspotRectangle.Rectangle.Y);
            face.TextureAngle = 0;
        }


        // Edge constraints:

        // TODO: Store face neighbor information inside Brush (it can be determined when creating a brush from face planes anyway)!
        private static ConcaveEdges GetEdgeConstraints(Face face, Brush parentBrush, FaceProjection faceProjection, HotspotSettings settings)
        {
            var edgeConstraints = ConcaveEdges.None;
            for (int i = 0; i < 4; i++)
            {
                var edge = faceProjection.AlignedEdges[i];
                if (edge == FaceProjection.NoEdge)
                    continue;

                var neighboringFace = GetNeighboringFace(face, parentBrush, edge);
                if (neighboringFace is null)
                    continue;

                // Edges with textures that are on the skip list are treated as concave:
                if (settings.IgnoreTextures.Contains(neighboringFace.TextureName))
                    edgeConstraints |= (ConcaveEdges)(1 << i);
            }
            return edgeConstraints;
        }

        // TODO: What's a good threshold?? 1/N (where N is a power of two, maybe 32 or 64 or so)?
        // NOTE: Edge is the index of the start vertex of the shared edge, in the given face -- so the edge from face.Vertices[startVertex] to face.Vertices[GetNextVertexIndex(face, startVertex)]
        private static Face? GetNeighboringFace(Face face, Brush parentBrush, int edge, double threshold = 0.125)
        {
            // NOTE: In the neighboring face, start and end vertices are inverted!
            var startVertex = face.Vertices[edge];
            var endVertex = face.Vertices[GetNextVertexIndex(face, edge)];

            foreach (var otherFace in parentBrush.Faces)
            {
                if (otherFace == face)
                    continue;

                for (int i = 0; i < otherFace.Vertices.Count; i++)
                {
                    if (IsSameVertex(endVertex, otherFace.Vertices[i], threshold))
                    {
                        if (IsSameVertex(startVertex, otherFace.Vertices[GetNextVertexIndex(otherFace, i)], threshold))
                            return otherFace;
                        else
                            break;
                    }
                }
            }

            return null;


            bool IsSameVertex(Vector3D vertex1, Vector3D vertex2, double threshold)
                => Math.Abs(vertex1.X - vertex2.X) < threshold && Math.Abs(vertex1.Y - vertex2.Y) < threshold && Math.Abs(vertex1.Z - vertex2.Z) < threshold;
        }

        private static int GetPreviousVertexIndex(Face face, int index)
            => index == 0 ? face.Vertices.Count - 1 : index - 1;

        private static int GetNextVertexIndex(Face face, int index)
            => index == face.Vertices.Count - 1 ? 0 : index + 1;


        // Scoring:

        // Returns a score that indicates how good of a match the given hotspot rectangle is for the given bounding box.
        private static HotspotRectangleScore GetBestHotspotScore(Rectangle boundingBox, ConcaveEdges edgeConstraints, HotspotRectangle hotspotRectangle, HotspotSettings settings)
        {
            var score = GetHotspotScoreWithoutRotation(boundingBox, edgeConstraints, hotspotRectangle, settings);
            var orientations = new List<HotspotOrientations> { HotspotOrientations.NoRotation | score.Orientations[0] };

            if (settings.AllowRotation && hotspotRectangle.AllowRotation)
            {
                var rotated180Score = GetHotspotScoreWithoutRotation(boundingBox, edgeConstraints.Rotate180(), hotspotRectangle, settings);
                var rotated180Orientation = rotated180Score.Orientations[0] | HotspotOrientations.Rotate180Degrees;
                if (rotated180Score.Score > score.Score)
                {
                    score = rotated180Score;
                    orientations = new List<HotspotOrientations> { rotated180Orientation };
                }
                else if (rotated180Score.Score == score.Score)
                {
                    orientations.Add(rotated180Orientation);
                }

                var rotatedBoundingBox = new Rectangle(0, 0, boundingBox.Height, boundingBox.Width);
                var rotatedLeftScore = GetHotspotScoreWithoutRotation(rotatedBoundingBox, edgeConstraints.Rotate270(), hotspotRectangle, settings);
                var rotatedLeftOrientation = rotatedLeftScore.Orientations[0] | HotspotOrientations.Rotate270Degrees;
                if (rotatedLeftScore.Score > score.Score)
                {
                    score = rotatedLeftScore;
                    orientations = new List<HotspotOrientations> { rotatedLeftOrientation };
                }
                else if (rotatedLeftScore.Score == score.Score)
                {
                    orientations.Add(rotatedLeftOrientation);
                }

                var rotatedRightScore = GetHotspotScoreWithoutRotation(rotatedBoundingBox, edgeConstraints.Rotate90(), hotspotRectangle, settings);
                var rotatedRightOrientation = rotatedRightScore.Orientations[0] | HotspotOrientations.Rotate90Degrees;
                if (rotatedRightScore.Score > score.Score)
                {
                    score = rotatedRightScore;
                    orientations = new List<HotspotOrientations> { rotatedRightOrientation };
                }
                else if (rotatedRightScore.Score == score.Score)
                {
                    orientations.Add(rotatedRightOrientation);
                }
            }

            return new HotspotRectangleScore(hotspotRectangle, score.Score, orientations);
        }

        // NOTE: This will try to mirror the rectangle to see if it can better match the given edge constraints, but it won't try different rotations.
        private static HotspotRectangleScore GetHotspotScoreWithoutRotation(Rectangle boundingBox, ConcaveEdges edgeConstraints, HotspotRectangle hotspotRectangle, HotspotSettings settings)
        {
            var texelScalingScore = GetTexelScalingScore(boundingBox, hotspotRectangle, settings);
            var edgeConstraintScore = GetEdgeConstraintScore(edgeConstraints, hotspotRectangle.ConcaveEdges);

            var mirroring = HotspotOrientations.NoMirroring;
            if (settings.AllowMirroring && hotspotRectangle.AllowMirroring)
            {
                var mirrorHorizontallyScore = GetEdgeConstraintScore(edgeConstraints.MirrorHorizontally(), hotspotRectangle.ConcaveEdges);
                if (mirrorHorizontallyScore > edgeConstraintScore)
                {
                    edgeConstraintScore = mirrorHorizontallyScore;
                    mirroring = HotspotOrientations.MirrorHorizontally;
                }
                else if (mirrorHorizontallyScore == edgeConstraintScore)
                {
                    mirroring |= HotspotOrientations.MirrorHorizontally;
                }

                var mirrorVerticallyScore = GetEdgeConstraintScore(edgeConstraints.MirrorVertically(), hotspotRectangle.ConcaveEdges);
                if (mirrorVerticallyScore > edgeConstraintScore)
                {
                    edgeConstraintScore = mirrorVerticallyScore;
                    mirroring = HotspotOrientations.MirrorVertically;
                }
                else if (mirrorVerticallyScore == edgeConstraintScore)
                {
                    mirroring |= HotspotOrientations.MirrorVertically;
                }
            }

            return new HotspotRectangleScore(hotspotRectangle, GetTotalScore(texelScalingScore, edgeConstraintScore), new[] { mirroring });
        }

        // 1 = perfect match, lower = more texel stretching/scaling. Score should always be above 0.
        private static double GetTexelScalingScore(Rectangle boundingBox, HotspotRectangle hotspotRectangle, HotspotSettings settings)
        {
            // NOTE: Tiling textures should have *some* penalty so that they score less than a perfect fitting rectangle, but not so much of a penalty that they'll be avoided altogether!
            var widthScore = 0.75;
            if (hotspotRectangle.TilingMode != TilingMode.Horizontal)
            {
                var scaledRectangleWidth = hotspotRectangle.Rectangle.Width * settings.DefaultTextureScale;
                if (boundingBox.Width < scaledRectangleWidth)
                    widthScore = boundingBox.Width / scaledRectangleWidth;
                else
                    widthScore = scaledRectangleWidth / boundingBox.Width;
            }

            var heightScore = 0.75;
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

        // 1 = perfect match, 0.5 = no match at all. Score should always be above 0.
        private static double GetEdgeConstraintScore(ConcaveEdges edgeConstraints, ConcaveEdges hotspotRectangleEdges)
        {
            var score = 1.0;

            var penalty = 0.125;
            if (edgeConstraints.HasFlag(ConcaveEdges.Top) != hotspotRectangleEdges.HasFlag(ConcaveEdges.Top)) score -= penalty;
            if (edgeConstraints.HasFlag(ConcaveEdges.Right) != hotspotRectangleEdges.HasFlag(ConcaveEdges.Right)) score -= penalty;
            if (edgeConstraints.HasFlag(ConcaveEdges.Bottom) != hotspotRectangleEdges.HasFlag(ConcaveEdges.Bottom)) score -= penalty;
            if (edgeConstraints.HasFlag(ConcaveEdges.Left) != hotspotRectangleEdges.HasFlag(ConcaveEdges.Left)) score -= penalty;

            return score;
        }

        // TODO: Maybe have a setting slider for adjusting the weight of each individual score?
        private static double GetTotalScore(double texelScalingScore, double edgeConstraintScore)
            => texelScalingScore * edgeConstraintScore;


        enum Axis
        {
            X,
            Y,
            Z,
        }

        class FaceProjection
        {
            public const int NoEdge = -1;

            public const int Top = 0;
            public const int Right = 1;
            public const int Bottom = 2;
            public const int Left = 3;


            public Vector3D RightAxis { get; }
            public Vector3D DownAxis { get; }
            public Rectangle BoundingBox { get; }

            // This must contain 4 values: [top, right, bottom, left]. Each value is either -1 (no matching edge) or the index of the starting vertex of an edge.
            public int[] AlignedEdges { get; }


            public FaceProjection(Vector3D rightAxis, Vector3D downAxis, Rectangle boundingBox, int[] alignedEdges)
            {
                RightAxis = rightAxis;
                DownAxis = downAxis;
                BoundingBox = boundingBox;
                AlignedEdges = alignedEdges.ToArray();
            }
        }

        [Flags]
        public enum HotspotOrientations : uint
        {
            NoRotation =            1,
            Rotate90Degrees =       2,
            Rotate180Degrees =      4,
            Rotate270Degrees =      8,

            RotationMask = NoRotation | Rotate90Degrees | Rotate180Degrees | Rotate270Degrees,

            NoMirroring =           16,
            MirrorHorizontally =    32,
            MirrorVertically =      64,

            MirrorMask = NoMirroring | MirrorHorizontally | MirrorVertically,
        }

        struct HotspotRectangleScore
        {
            public HotspotRectangle HotspotRectangle { get; }
            public double Score { get; }
            public HotspotOrientations[] Orientations { get; }


            public HotspotRectangleScore(HotspotRectangle hotspotRectangle, double score, IEnumerable<HotspotOrientations> orientations)
            {
                HotspotRectangle = hotspotRectangle;
                Score = score;
                Orientations = orientations.ToArray();
            }
        }
    }
}
