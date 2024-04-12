﻿namespace MESS.Formats.RMF
{
    public class RmfFileSaveSettings : FileSaveSettings
    {
        public RmfFileVersion FileVersion { get; set; } = RmfFileVersion.V2_2;


        public RmfFileSaveSettings(FileSaveSettings? settings = null)
            : base(settings)
        {
        }
    }
}
