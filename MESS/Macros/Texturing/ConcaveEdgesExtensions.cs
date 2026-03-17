namespace MESS.Macros.Texturing
{
    public static class ConcaveEdgesExtensions
    {
        public static ConcaveEdges MirrorHorizontally(this ConcaveEdges edges)
            => (ConcaveEdges)(((int)edges & 0x05) | (((int)edges & 0x02) << 2) | ((int)edges & 0x08) >> 2);

        public static ConcaveEdges MirrorVertically(this ConcaveEdges edges)
            => (ConcaveEdges)(((int)edges & 0x0A) | (((int)edges & 0x01) << 2) | ((int)edges & 0x04) >> 2);

        public static ConcaveEdges Rotate90(this ConcaveEdges edges)
            => (ConcaveEdges)((((int)edges & 0x07) << 1) | ((int)edges & 0x08) >> 3);

        public static ConcaveEdges Rotate180(this ConcaveEdges edges)
            => (ConcaveEdges)((((int)edges & 0x03) << 2) | ((int)edges & 0x0C) >> 2);

        public static ConcaveEdges Rotate270(this ConcaveEdges edges)
            => (ConcaveEdges)((((int)edges & 0x01) << 3) | ((int)edges & 0x0E) >> 1);
    }
}
