using HotspotMaker.History;
using MLib.Texturing.Hotspotting;
using System.Collections.Generic;

namespace HotspotMaker.Hotspot
{
    public class HotspotBindingVM : ChangeTrackingVM
    {
        private string _textureNamePattern = "";
        public string TextureNamePattern
        {
            get => _textureNamePattern;
            set => SetPropertyOngoing(v => _textureNamePattern = v, _textureNamePattern, value);
        }

        private string _hotspotName = "";
        public string HotspotName
        {
            get => _hotspotName;
            set => SetPropertyOngoing(v => _hotspotName = v, _hotspotName, value);
        }

        private string? _fallbackTextureNamePattern;
        public string? FallbackTextureNamePattern
        {
            get => _fallbackTextureNamePattern;
            set => SetPropertyOngoing(v => _fallbackTextureNamePattern = v, _fallbackTextureNamePattern, value);
        }

        private double? _fallbackScoreThreshold;
        public double? FallbackScoreThreshold
        {
            get => _fallbackScoreThreshold;
            set => SetPropertyOngoing(v => _fallbackScoreThreshold = v, _fallbackScoreThreshold, value);
        }

        public List<string> Labels { get; } = new();


        public HotspotBindingVM(string textureNamePattern, string hotspotName, UndoSystem undoSystem)
            : base(undoSystem)
        {
            WithoutChangeTracking(() =>
            {
                TextureNamePattern = textureNamePattern;
                HotspotName = hotspotName;
            });
        }

        public HotspotBindingVM(HotspotBinding binding, UndoSystem undoSystem)
            : base(undoSystem)
        {
            WithoutChangeTracking(() =>
            {
                TextureNamePattern = binding.TextureNamePattern;
                HotspotName = binding.HotspotName;

                FallbackTextureNamePattern = binding.FallbackTextureNamePattern;
                FallbackScoreThreshold = binding.FallbackScoreThreshold;

                Labels.AddRange(binding.Labels);
            });
        }

        public HotspotBinding CreateHotspotBinding()
        {
            return new HotspotBinding(
                TextureNamePattern,
                HotspotName,
                FallbackTextureNamePattern,
                FallbackScoreThreshold ?? 0,
                Labels);
        }
    }
}
