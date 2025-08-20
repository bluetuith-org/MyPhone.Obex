using System.Collections.Generic;
using System.Xml;

namespace GoodTimeStudio.MyPhone.OBEX.Map;

public class MessageListing
{
    public static readonly List<string> MessageListingObjects =
    [
        "handle",
        "size",
        "type",
        "subject",
        "datetime",
        "reception_status",
        "attachment_size",
        "recipient_addressing",
        // Optional/Implied
        "sender_name",
        "sender_addressing",
        "replyto_addressing",
        "recipient_name",
        "text",
        "priority",
        "read",
        "sent",
        "protected",
    ];

    public string Handle { get; set; } = "";
    public string Type { get; set; } = "";
    public int Size { get; set; }

    public string Subject { get; set; } = "";
    public string DateTime { get; set; } = "";

    public string SenderName { get; set; } = "";
    public string SenderAddressing { get; set; } = "";
    public string ReplyToAddressing { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string RecipientAddressing { get; set; } = "";

    public string ReceptionStatus { get; set; } = "";
    public int AttachmentSize { get; set; }

    public bool Text { get; set; }
    public bool Priority { get; set; }
    public bool Read { get; set; }
    public bool Sent { get; set; }
    public bool Received { get; set; } = false;
    public bool Protected { get; set; }

    public static List<MessageListing> Parse(string xmlDoc)
    {
        var messageList = new List<MessageListing>();

        var xml = new XmlDocument();
        xml.LoadXml(xmlDoc);

        var list = xml.SelectNodes("/MAP-msg-listing/msg");
        if (list == null)
            return messageList;

        foreach (XmlNode node in list)
        {
            var message = new MessageListing();

            foreach (var key in MessageListingObjects)
            {
                var singleNode = node.SelectSingleNode("@" + key);
                if (singleNode == null || string.IsNullOrEmpty(singleNode.Value))
                    continue;

                try
                {
                    switch (key)
                    {
                        case "handle":
                            message.Handle = singleNode.Value;
                            break;
                        case "size":
                            message.Size = int.Parse(singleNode.Value);
                            break;
                        case "type":
                            message.Type = singleNode.Value;
                            break;
                        case "subject":
                            message.Subject = singleNode.Value;
                            break;
                        case "datetime":
                            message.DateTime = singleNode.Value;
                            break;
                        case "reception_status":
                            message.ReceptionStatus = singleNode.Value;
                            break;
                        case "attachment_size":
                            message.AttachmentSize = int.Parse(singleNode.Value);
                            break;
                        case "recipient_addressing":
                            message.RecipientAddressing = singleNode.Value;
                            break;
                        case "sender_name":
                            message.SenderName = singleNode.Value;
                            break;
                        case "sender_addressing":
                            message.SenderAddressing = singleNode.Value;
                            break;
                        case "replyto_addressing":
                            message.ReplyToAddressing = singleNode.Value;
                            break;
                        case "recipient_name":
                            message.RecipientName = singleNode.Value;
                            break;
                        case "text":
                            message.Text = singleNode.Value == "yes" ? true : false;
                            break;
                        case "priority":
                            message.Priority = singleNode.Value == "yes" ? true : false;
                            break;
                        case "read":
                            message.Read = singleNode.Value == "yes" ? true : false;
                            break;
                        case "sent":
                            message.Sent = singleNode.Value == "yes" ? true : false;
                            break;
                        case "protected":
                            message.Protected = singleNode.Value == "yes" ? true : false;
                            break;
                    }
                }
                catch { }
            }

            messageList.Add(message);
        }

        return messageList;
    }
}
