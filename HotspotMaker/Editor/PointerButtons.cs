using System;

namespace HotspotMaker.Editor
{
    [Flags]
    public enum PointerButtons
    {
        None =      0x00,

        Left =      0x01,
        Middle =    0x02,
        Right =     0x04,
        X1 =        0x08,
        X2 =        0x10,
    }
}
