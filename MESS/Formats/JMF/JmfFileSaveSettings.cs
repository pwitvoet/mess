﻿namespace MESS.Formats.JMF
{
    public class JmfFileSaveSettings : FileSaveSettings
    {
        public JmfFileVersion FileVersion { get; set; } = JmfFileVersion.V121;


        public JmfFileSaveSettings(FileSaveSettings? settings = null)
            : base(settings)
        {
        }
    }
}
