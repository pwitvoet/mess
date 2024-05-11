using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Geometry
{
    /// <summary>
    /// Methods for generating basic and composite shapes.
    /// </summary>
    public static class Shapes
    {
        /// <summary>
        /// Creates a 6-sided axis-aligned block. Textures are aligned to the nearest axis.
        /// </summary>
        public static Brush Block(BoundingBox size, string texture)
        {
            var right = new Vector3D(1, 0, 0);
            var forward = new Vector3D(0, 1, 0);
            var back = new Vector3D(0, -1, 0);
            var down = new Vector3D(0, 0, -1);

            var scale = new Vector2D(1, 1);

            var faces = new[] {
                // Top:
                new Face {
                    PlanePoints = new[] {
                        new Vector3D(size.Min.X, size.Max.Y, size.Max.Z),
                        new Vector3D(size.Max.X, size.Max.Y, size.Max.Z),
                        new Vector3D(size.Max.X, size.Min.Y, size.Max.Z),
                    },
                    TextureName = texture,
                    TextureRightAxis = right,
                    TextureDownAxis = back,
                    TextureScale = scale,
                },
                // Bottom:
                new Face {
                    PlanePoints = new[] {
                        new Vector3D(size.Min.X, size.Min.Y, size.Min.Z),
                        new Vector3D(size.Max.X, size.Min.Y, size.Min.Z),
                        new Vector3D(size.Max.X, size.Max.Y, size.Min.Z),
                    },
                    TextureName = texture,
                    TextureRightAxis = right,
                    TextureDownAxis = back,
                    TextureScale = scale,
                },
                // Front:
                new Face {
                    PlanePoints = new[] {
                        new Vector3D(size.Min.X, size.Min.Y, size.Max.Z),
                        new Vector3D(size.Max.X, size.Min.Y, size.Max.Z),
                        new Vector3D(size.Max.X, size.Min.Y, size.Min.Z),
                    },
                    TextureName = texture,
                    TextureRightAxis = right,
                    TextureDownAxis = down,
                    TextureScale = scale,
                },
                // Back:
                new Face {
                    PlanePoints = new[] {
                        new Vector3D(size.Max.X, size.Max.Y, size.Max.Z),
                        new Vector3D(size.Min.X, size.Max.Y, size.Max.Z),
                        new Vector3D(size.Min.X, size.Max.Y, size.Min.Z),
                    },
                    TextureName = texture,
                    TextureRightAxis = right,
                    TextureDownAxis = down,
                    TextureScale = scale,
                },
                // Left:
                new Face {
                    PlanePoints = new[] {
                        new Vector3D(size.Min.X, size.Max.Y, size.Max.Z),
                        new Vector3D(size.Min.X, size.Min.Y, size.Max.Z),
                        new Vector3D(size.Min.X, size.Min.Y, size.Min.Z),
                    },
                    TextureName = texture,
                    TextureRightAxis = forward,
                    TextureDownAxis = down,
                    TextureScale = scale,
                },
                // Right:
                new Face {
                    PlanePoints = new[] {
                        new Vector3D(size.Max.X, size.Min.Y, size.Max.Z),
                        new Vector3D(size.Max.X, size.Max.Y, size.Max.Z),
                        new Vector3D(size.Max.X, size.Max.Y, size.Min.Z),
                    },
                    TextureName = texture,
                    TextureRightAxis = forward,
                    TextureDownAxis = down,
                    TextureScale = scale,
                },
            };
            return new Brush(faces);
        }
    }
}
