using System;

namespace GoodTimeStudio.MyPhone.OBEX.Pbap;

[Flags]
public enum PbapSupportedFeatures
{
    None = 0,
    Download = 1,
    Browsing = 2,
    DatabaseIdentifier = 4,
    FolderVersionCounters = 8,
    VCardSelecting = 16,
    EnhancedMissedCalls = 32,
    XBtUciVCardProperty = 64,
    XBtUidVCardProperty = 128,
    ContactReferencing = 256,
    DefaultContactImageFormat = 512,
}
