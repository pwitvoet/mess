using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Formats.JMF
{
    public class JmfEntity : Entity
    {
        public JmfMapObjectFlags JmfFlags { get; set; }
        public Vector3D JmfOrigin { get; set; }
        public List<string?> SpecialAttributeNames { get; } = new();     // TODO: ...
        public int JmfSpawnflags { get; set; }
        public Angles JmfAngles { get; set; }
        public JmfRenderMode JmfRendering { get; set; }

        public Color JmfFxColor { get; set; }
        public int JmfRenderMode { get; set; }
        public int JmfRenderFX { get; set; }
        public int JmfBody { get; set; }
        public int JmfSkin { get; set; }
        public int JmfSequence { get; set; }
        public float JmfFramerate { get; set; }
        public float JmfScale { get; set; }
        public float JmfRadius { get; set; }
        public byte[]? UnknownData { get; set; }

        public List<KeyValuePair<string?, string?>> JmfProperties { get; } = new();   // TODO: Original order!!!


        public JmfEntity(IEnumerable<Brush>? brushes = null)
            : base(brushes)
        {
        }

        public override Entity PartialCopy()
        {
            var copy = new JmfEntity();
            PartialCopyTo(copy);

            copy.JmfFlags = JmfFlags;
            copy.JmfOrigin = JmfOrigin;
            copy.SpecialAttributeNames.AddRange(SpecialAttributeNames);
            copy.JmfSpawnflags = JmfSpawnflags;
            copy.JmfAngles = JmfAngles;
            copy.JmfRendering = JmfRendering;

            copy.JmfFxColor = JmfFxColor;
            copy.JmfRenderMode = JmfRenderMode;
            copy.JmfRenderFX = JmfRenderFX;
            copy.JmfBody = JmfBody;
            copy.JmfSkin = JmfSkin;
            copy.JmfSequence = JmfSequence;
            copy.JmfFramerate = JmfFramerate;
            copy.JmfScale = JmfScale;
            copy.JmfRadius = JmfRadius;
            copy.UnknownData = UnknownData?.ToArray();

            copy.JmfProperties.AddRange(JmfProperties);

            return copy;
        }
    }
}
