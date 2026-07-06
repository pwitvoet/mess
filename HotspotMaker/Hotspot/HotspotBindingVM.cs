using HotspotMaker.History;
using MLib.Texturing.Hotspotting;
using System;
using System.Linq;

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

        private string[] _labels = Array.Empty<string>();
        public string[] Labels
        {
            get => _labels;
            set => SetPropertyOngoing(v => _labels = v, _labels, value);
        }


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

                Labels = binding.Labels.ToArray();
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
