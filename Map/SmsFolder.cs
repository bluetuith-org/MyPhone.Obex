using System.Collections.Generic;

namespace GoodTimeStudio.MyPhone.OBEX.Map;

public class SmsFolder(string folderName, int messageCount, SmsFolder? parent = null)
{
    public SmsFolder(string folderName, SmsFolder? parent = null)
        : this(folderName, 0, parent) { }

    public string Name { get; } = folderName;

    /// <summary>
    ///     Number of accessible messages in this folder
    /// </summary>
    public int MessageCount { get; } = messageCount;

    public IList<SmsFolder> Children { get; } = new List<SmsFolder>();
    public IList<MessageListing> MessageHandles { get; } = new List<MessageListing>();

    public SmsFolder? Parent { get; } = parent;
}
