using MixERP.Net.VCards;
using System;
using System.Collections.Generic;

namespace GoodTimeStudio.MyPhone.OBEX
{
    public class BMessage
    {

        public BMessageStatus Status { get; set; }

        public string Type { get; set; }

        public string Folder { get; set; }

        public VCard Sender { get; set; }

        public string Charset { get; set; }

        public int Length { get; set; }

        public string Body { get; set; }

        public BMessage(BMessageStatus status, string type, string folder, VCard sender, string charset, int length, string body)
        {
            Status = status;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
            Sender = sender;
            Charset = charset ?? throw new ArgumentNullException(nameof(charset));
            Length = length;
            Body = body ?? throw new ArgumentNullException(nameof(body));
        }

        private BMessage() { }

        public static BMessage Parse(string bMsg)
        {
            BMessage bMessage = new();
            BMessageNode bMsgNode = BMessageNode.Parse(bMsg);

            foreach (var item in new List<string>() {
                "STATUS", "TYPE", "FOLDER", "CHARSET", "LENGTH", "MSG", "VCARD"
            })
            {
                switch (item)
                {
                    case "STATUS":
                        if (bMsgNode.Attributes.TryGetValue(item, out var status))
                            bMessage.Status = status == "UNREAD" ? BMessageStatus.UNREAD : BMessageStatus.READ;
                        break;
                    case "TYPE":
                        if (bMsgNode.Attributes.TryGetValue(item, out var type))
                            bMessage.Type = type;
                        break;
                    case "FOLDER":
                        if (bMsgNode.Attributes.TryGetValue(item, out var folder))
                            bMessage.Folder = folder;
                        break;
                    case "CHARSET":
                        if (TryGetAttributeFromBodyNode(bMsgNode, false, item, out var charset))
                            bMessage.Charset = charset;
                        break;
                    case "LENGTH":
                        if (TryGetAttributeFromBodyNode(bMsgNode, false, item, out var length))
                            bMessage.Length = int.Parse(length);
                        break;
                    case "MSG":
                        if (TryGetAttributeFromBodyNode(bMsgNode, true, item, out var msgValue))
                            bMessage.Body = msgValue;
                        break;
                    case "VCARD":
                        if (TryGetAttributeFromBodyNode(bMsgNode, true, item, out var vcardValue))
                            bMessage.Sender = Deserializer.GetVCard(vcardValue);
                        break;
                }
            }

            return bMessage;
        }

        private static bool TryGetAttributeFromBodyNode(BMessageNode bMsgNode, bool itemIsChildNode, string item, out string value)
        {
            if (bMsgNode.ChildrenNode.TryGetValue("BENV", out var benv))
                if (benv.ChildrenNode.TryGetValue("BBODY", out var bbody))
                {
                    if (itemIsChildNode)
                    {
                        if (bbody.ChildrenNode.TryGetValue(item, out var childNode) &&
                            childNode != null &&
                            !string.IsNullOrEmpty(childNode.Value))
                        {
                            if (item == "VCARD")
                                value = childNode.ToString();
                            else
                                value = childNode.Value;

                            return true;
                        }
                    }
                    else
                    {
                        if (bbody.Attributes.TryGetValue(item, out var attribute) &&
                            attribute != null &&
                            !string.IsNullOrEmpty(attribute))
                        {
                            value = attribute;
                            return true;
                        }
                    }
                }

            value = string.Empty;

            return false;
        }
    }
}

public enum BMessageStatus
{
    UNREAD,
    READ
}
