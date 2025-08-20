using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoodTimeStudio.MyPhone.OBEX.Headers;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX.Opp;

/// <summary>
///     Phone Book Access Profile client
/// </summary>
public class OppClient : ObexClient
{
    private ushort _clientPacketSize = 256;

    /// <remarks>
    ///     Not null after connected.
    /// </remarks>
    private ObexHeader? _connectionIdHeader;

    public EventHandler<TransferEventData>? TransferEventHandler;

    public OppClient(
        IInputStream inputStream,
        IOutputStream outputStream,
        CancellationTokenSource token
    )
        : base(inputStream, outputStream, token) { }

    protected override void OnConnected(ObexPacket connectionResponse)
    {
        _connectionIdHeader = connectionResponse.GetHeader(HeaderId.ConnectionId);

        _clientPacketSize = Math.Max(
            _clientPacketSize,
            (ushort)(
                ((ObexConnectPacket)connectionResponse).MaximumPacketLength - _clientPacketSize
            )
        );
    }

    public void CancelTransfer()
    {
        CancellationTokenSource.Cancel();
    }

    public void Refresh()
    {
        CancellationTokenSource = new CancellationTokenSource();
    }

    public async Task SendFile(string fileName)
    {
        CancellationTokenSource.Token.ThrowIfCancellationRequested();

        var file = new FileInfo(fileName);
        var filename = Path.GetFileName(file.Name);

        using FileStream fileStream = new(
            fileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            _clientPacketSize,
            true
        );

        var buffer = new byte[_clientPacketSize];
        var bodyHeader = new ObexHeader(HeaderId.Body, buffer);
        var eobHeader = new ObexHeader(HeaderId.EndOfBody, buffer);

        var data = new TransferEventData
        {
            Name = filename,
            FileName = fileName,
            FileSize = file.Length,
        };

        var request = new ObexPacket(
            new ObexOpcode(ObexOperation.Put, false),
            _connectionIdHeader!,
            new ObexHeader(HeaderId.Name, filename, true, Encoding.BigEndianUnicode),
            new ObexHeader(HeaderId.Length, (int)file.Length),
            new ObexHeader(HeaderId.Type, MimeTypes.GetMimeType(file.Name), true, Encoding.ASCII),
            bodyHeader
        );

        var completed = false;
        var packetModified = false;
        var firstPut = true;

        try
        {
            int bytesRead;
            var totalRead = 0;

            while ((bytesRead = fileStream.Read(buffer, 0, _clientPacketSize)) > 0)
            {
                totalRead += bytesRead;

                if (totalRead > 0 && !firstPut && !packetModified)
                {
                    request = new ObexPacket(new ObexOpcode(ObexOperation.Put, false), bodyHeader);

                    data.Name = data.FileName = "";
                    packetModified = true;
                }

                if (CancellationTokenSource.IsCancellationRequested)
                {
                    await AbortAsync();

                    return;
                }

                if (bytesRead != _clientPacketSize && totalRead == file.Length)
                {
                    eobHeader.Buffer = buffer[..bytesRead];

                    request.Opcode = new ObexOpcode(ObexOperation.Put, true);
                    request.ReplaceHeader(HeaderId.Body, eobHeader);
                }

                request.WriteToStream(Writer);
                await Writer.StoreAsync();

                var subResponse = await ObexPacket.ReadFromStream(Reader);
                switch (subResponse.Opcode.ObexOperation)
                {
                    case ObexOperation.Success:
                        completed = true;
                        break;

                    case ObexOperation.Continue:
                        break;

                    default:
                        throw new ObexException(
                            $"Operation code: {subResponse.Opcode.ObexOperation}"
                        );
                }

                if (firstPut && data.BytesTransferred == 0)
                    SendObexTransferEvent(data with { Queued = true });

                data.BytesTransferred = totalRead;
                SendObexTransferEvent(data);

                firstPut = false;
            }
        }
        finally
        {
            data.Error = !completed;
            data.TransferDone = true;
            SendObexTransferEvent(data);
        }
    }

    private void SendObexTransferEvent(TransferEventData eventData)
    {
        TransferEventHandler?.Invoke(this, eventData);
    }

    public record TransferEventData
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public long BytesTransferred { get; set; }
        public bool TransferDone { get; set; }
        public bool Error { get; set; }

        public bool Queued { get; set; }
    }
}
