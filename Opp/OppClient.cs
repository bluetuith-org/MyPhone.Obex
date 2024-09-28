using GoodTimeStudio.MyPhone.OBEX.Headers;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    /// <summary>
    /// Phone Book Access Profile client
    /// </summary>
    public class OppClient : ObexClient
    {
        /// <remarks>
        /// Not null after connected.
        /// </remarks>
        private ObexHeader? _connectionIdHeader;

        private bool IsConnectionClosed = false;
        private StreamSocket _socket;
        private CancellationTokenSource _cancellationTokenSource = new();

        private ushort _clientPacketSize = 256;

        public record TransferEventData(string FileName, long FileSize, long BytesTransferred, bool TransferDone);
        public EventHandler<TransferEventData>? TransferEventHandler;

        public OppClient(StreamSocket socket) : base(socket.InputStream, socket.OutputStream)
        {
            _socket = socket;
        }

        protected override void OnConnected(ObexPacket connectionResponse)
        {
            _connectionIdHeader = connectionResponse.GetHeader(HeaderId.ConnectionId);

            _clientPacketSize = Math.Max(
                _clientPacketSize,
                (ushort)(((ObexConnectPacket)connectionResponse).MaximumPacketLength - _clientPacketSize)
            );
        }

        public async Task SendFiles(params string[] files)
        {
            foreach (var file in files)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                if (IsConnectionClosed)
                    throw new ObexException("The socket has been closed");

                if (!await SendFile(file))
                    break;
            }
        }

        public void StopSendingFiles()
        {
            _cancellationTokenSource.Cancel();
        }

        private async Task<bool> SendFile(string fileName)
        {
            FileInfo file = new FileInfo(fileName);

            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, _clientPacketSize, true))
            using (var ms = new MemoryStream(_clientPacketSize))
            {
                {
                    var filename = Path.GetFileName(file.Name);
                    long numBytes = file.Length;

                    ObexPacket request = new ObexPacket(new ObexOpcode(ObexOperation.Put, false),
                        _connectionIdHeader!,
                        new ObexHeader(HeaderId.Name, filename, true, Encoding.BigEndianUnicode),
                        new ObexHeader(HeaderId.Length, (int)file.Length),
                        new ObexHeader(HeaderId.Type, MimeTypes.GetMimeType(file.Name), true, Encoding.ASCII)
                    );

                    ms.SetLength(_clientPacketSize);

                    do
                    {
                        ms.Position = 0;

                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            request = new ObexPacket(new ObexOpcode(ObexOperation.Abort, true));
                            _writer.WriteBuffer(request.ToBuffer());
                            await _writer.StoreAsync();

                            _socket.Dispose();
                            IsConnectionClosed = true;

                            SendObexTransferEvent(new TransferEventData(
                                filename, file.Length, file.Length - numBytes, true
                            ));

                            return false;
                        }

                        if (numBytes <= (_clientPacketSize))
                        {
                            ms.SetLength(numBytes);
                            fileStream.Read(ms.GetBuffer());

                            request.Opcode = new ObexOpcode(ObexOperation.Put, true);
                            request.ReplaceHeader(HeaderId.Body, new ObexHeader(HeaderId.EndOfBody, ms.ToArray()));
                        }
                        else
                        {
                            fileStream.Read(ms.GetBuffer());

                            if (request.Headers.TryGetValue(HeaderId.Body, out var header))
                                header.Buffer = ms.ToArray();
                            else
                                request.AddHeader(new ObexHeader(HeaderId.Body, ms.ToArray()));
                        }

                        numBytes -= ms.Length;

                        var buf = request.ToBuffer();
                        _writer.WriteBuffer(buf);
                        await _writer.StoreAsync();

                        ObexPacket subResponse;
                        subResponse = await ObexPacket.ReadFromStream(_reader);

                        SendObexTransferEvent(new TransferEventData(
                            filename, file.Length, file.Length - numBytes,
                            subResponse.Opcode.ObexOperation != ObexOperation.Continue
                        ));

                        switch (subResponse.Opcode.ObexOperation)
                        {
                            case ObexOperation.Success:
                                goto done;

                            case ObexOperation.Continue:
                                break;

                            default:
                                throw new ObexException($"Operation code: {subResponse.Opcode.ObexOperation}");
                        }
                    } while (numBytes > 0);
                }

            done:
                return true;
            }
        }


        private void SendObexTransferEvent(TransferEventData eventData)
        {
            TransferEventHandler?.Invoke(this, eventData);
        }
    }
}
