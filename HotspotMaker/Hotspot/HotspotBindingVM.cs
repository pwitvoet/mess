using MLib.Texturing.Hotspotting;
using System.Collections.Generic;

namespace HotspotMaker.Hotspot
{
    public class HotspotBindingVM
    {
        public string TextureNamePattern { get; set; } = "";
        public string HotspotName { get; set; } = "";

        public string? FallbackTextureNamePattern { get; set; }
        public double? FallbackScoreThreshold { get; set; }

        public List<string> Labels { get; } = new();


        public HotspotBindingVM()
        {
        }

        public HotspotBindingVM(HotspotBinding binding)
        {
            TextureNamePattern = binding.TextureNamePattern;
            HotspotName = binding.HotspotName;

            FallbackTextureNamePattern = binding.FallbackTextureNamePattern;
            FallbackScoreThreshold = binding.FallbackScoreThreshold;

            Labels.AddRange(binding.Labels);
        }
    }
}
