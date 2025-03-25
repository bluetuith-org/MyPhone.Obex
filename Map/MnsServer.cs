using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX
{
    public class MessageReceivedEventArgs
    {
        public string EventType { get; set; } = "";
        public string MessageType { get; set; } = "";
        public string MessageHandle { get; set; } = "";
        public string Folder { get; set; } = "";

        public MessageReceivedEventArgs(
            string eventType,
            string messageType,
            string folder,
            string messageHandle
        )
        {
            EventType = eventType;
            MessageType = messageType;
            Folder = folder;
            MessageHandle = messageHandle;
        }

        public MessageReceivedEventArgs() { }

        public static MessageReceivedEventArgs Parse(string xmlDoc)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlDoc);

            var messageEventArgs = new MessageReceivedEventArgs();

            foreach (var item in new List<string>() { "handle", "type", "folder", "msg_type" })
            {
                var singleNode = doc.SelectSingleNode("/MAP-event-report/event/@" + item);
                if (singleNode == null || singleNode.Value == null)
                    if (item == "handle")
                        return null;
                    else
                        continue;

                switch (item)
                {
                    case "handle":
                        messageEventArgs.MessageHandle = singleNode.Value;
                        break;
                    case "type":
                        messageEventArgs.EventType = singleNode.Value;
                        break;
                    case "folder":
                        messageEventArgs.Folder = singleNode.Value;
                        break;
                    case "msg_type":
                        messageEventArgs.MessageType = singleNode.Value;
                        break;
                }
            }

            return messageEventArgs;
        }
    }

    public class MnsServer : ObexServer
    {
        public MnsServer(
            IInputStream inputStream,
            IOutputStream outputStream,
            CancellationTokenSource token
        )
            : base(inputStream, outputStream, ObexServiceUuid.MessageNotification, token) { }

        public delegate void MnsMessageReceivedEventHandler(
            object sender,
            MessageReceivedEventArgs e
        );
        public event MnsMessageReceivedEventHandler? MessageReceived;

        protected override ObexPacket? OnClientRequest(ObexPacket clientRequestPacket)
        {
            _cts.Token.ThrowIfCancellationRequested();

            if (clientRequestPacket.Opcode.ObexOperation == ObexOperation.Put)
            {
                if (!clientRequestPacket.Headers.TryGetValue(HeaderId.Body, out var body))
                    return null;
                if (body.Buffer.Length == 0)
                    return null;

                var eventXml = Encoding.UTF8.GetString(body.Buffer);
                var messageEventArgs = MessageReceivedEventArgs.Parse(eventXml);
                if (messageEventArgs == null)
                    return null;

                MessageReceived?.Invoke(this, messageEventArgs);
            }

            return clientRequestPacket;
        }
    }
}
