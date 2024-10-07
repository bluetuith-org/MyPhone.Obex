﻿using System.Threading;
using System.Xml;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX
{
    public class MessageReceivedEventArgs
    {
        public string MessageHandle { get; set; }

        public MessageReceivedEventArgs(string messageHandle)
        {
            MessageHandle = messageHandle;
        }
    }

    public class MnsServer : ObexServer
    {
        public MnsServer(IInputStream inputStream, IOutputStream outputStream, CancellationTokenSource token) : base(inputStream, outputStream, ObexServiceUuid.MessageNotification, token)
        { }

        public delegate void MnsMessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event MnsMessageReceivedEventHandler? MessageReceived;

        protected override ObexPacket? OnClientRequest(ObexPacket clientRequestPacket)
        {
            _cts.Token.ThrowIfCancellationRequested();

            if (clientRequestPacket.Opcode.ObexOperation == ObexOperation.Put)
            {

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(clientRequestPacket.GetBodyContentAsUtf8String(true));
                string? handle = doc.SelectSingleNode("/MAP-event-report/event/@handle")?.Value;

                if (handle != null)
                {
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(handle));
                    return new ObexPacket(new ObexOpcode(ObexOperation.Success, true));
                }
            }

            return null;
        }
    }
}
