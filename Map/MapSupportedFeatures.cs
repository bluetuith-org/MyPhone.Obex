using System;

namespace GoodTimeStudio.MyPhone.OBEX.Map;

[Flags]
public enum MapSupportedFeatures
{
    None = 0,
    NotificationRegistration = 1,
    Notification = 2,
    Browsing = 4,
    Uploading = 8,
    Delete = 16,
    InstanceInformation = 32,
    ExtendedEventReportV11 = 64,
    EventReportV12 = 128,
    MessageFormatV11 = 256,
    MessagesListingFormatV11 = 512,
    PersistentMessageHandles = 1024,
    DatabaseIdentifier = 2048,
    FolderVersionCounter = 4096,
    ConversationVersionCounters = 8192,
    ParticipantPresenceChangeNotification = 16384,
    ParticipantChatStateChangeNotification = 32768,
    PbapContactCrossReference = 65536,
    NotificationFiltering = 131072,
    UtcOffsetTimestampFormat = 262144,
    MapSupportedFeaturesInConnectRequest = 524288,
    ConversationListing = 1048576,
    OwnerStatus = 2097152,

    MessageForwarding = 4194304,
    // As of MAP v1.4.2
}
