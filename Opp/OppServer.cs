using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class OppServer : ObexServer
    {
        private bool IsConnectionClosed = false;
        private readonly StreamSocket _socket;
        private readonly CancellationTokenSource _transfercts;

        private string _destinationDirectory { get; set; } = "";

        public record ReceiveTransferEventData
        {
            public string FileName = "";
            public string TempFileName = "";
            public long FileSize;
            public long BytesTransferred;
            public bool TransferDone;
            public bool Error;
        };

        public EventHandler<ReceiveTransferEventData>? ReceiveTransferEventHandler;

        public OppServer(
            StreamSocket socket,
            CancellationTokenSource token,
            string destinationDirectory = ""
        )
            : base(socket.InputStream, socket.OutputStream, ObexServiceUuid.ObjectPush, token)
        {
            _socket = socket;
            _transfercts = CancellationTokenSource.CreateLinkedTokenSource(token.Token);
            _destinationDirectory = destinationDirectory;
        }

        public override void CancelTransfer()
        {
            _transfercts.Cancel();
        }

        public override async Task Run()
        {
            Exception? exception = null;

            if (IsConnectionClosed)
                return;

            listenForConnections:
            _transfercts.Token.ThrowIfCancellationRequested();

            ObexPacket packet = await ObexPacket.ReadFromStream<ObexConnectPacket>(_reader);
            switch (packet.Opcode.ObexOperation)
            {
                case ObexOperation.Connect:
                    packet.Opcode = new ObexOpcode(ObexOperation.Success, true);
                    _writer.WriteBuffer(packet.ToBuffer());
                    await _writer.StoreAsync();
                    break;
                default:
                    packet = new ObexPacket(new ObexOpcode(ObexOperation.ServiceUnavailable, true));
                    _writer.WriteBuffer(packet.ToBuffer());
                    goto listenForConnections;
            }

            _transfercts.Token.ThrowIfCancellationRequested();

            ReceiveTransferEventData data = new();
            var fileDeleted = false;

            using (var file = File.OpenWrite(Path.GetTempFileName()))
            {
                while (true)
                {
                    if (_transfercts.Token.IsCancellationRequested)
                    {
                        packet.Opcode = new ObexOpcode(ObexOperation.Abort, true);
                        _writer.WriteBuffer(packet.ToBuffer());
                        await _writer.StoreAsync();

                        _socket.Dispose();
                        IsConnectionClosed = true;

                        goto deleteFile;
                    }

                    data.TempFileName = file.Name;
                    var sendPacket = new ObexPacket(new ObexOpcode(ObexOperation.Success, true));

                    packet = await ObexPacket.ReadFromStream(_reader);
                    switch (packet.Opcode.ObexOperation)
                    {
                        case ObexOperation.Put:
                            if (!packet.Opcode.IsFinalBitSet)
                                sendPacket = new ObexPacket(
                                    new ObexOpcode(ObexOperation.Continue, true)
                                );

                            _writer.WriteBuffer(sendPacket.ToBuffer());
                            await _writer.StoreAsync();

                            var packetInfo = GetPacketInfo(packet);
                            var (pFileName, pFileSize, pBufferLength) = (
                                packetInfo.FileName,
                                packetInfo.FileSize,
                                packetInfo.buffer.Length
                            );
                            if (pFileName != "")
                                data.FileName = packetInfo.FileName;
                            if (pFileSize > 0)
                                data.FileSize = packetInfo.FileSize;
                            if (pBufferLength > 0)
                                data.BytesTransferred += packetInfo.buffer.Length;

                            file.Write(packetInfo.buffer);
                            SendObexReceiveEvent(data);

                            if (packetInfo.IsFinal)
                                goto finishTransfer;

                            continue;

                        case ObexOperation.Abort:
                        case ObexOperation.Disconnect:
                            _writer.WriteBuffer(sendPacket.ToBuffer());
                            await _writer.StoreAsync();

                            _socket.Dispose();
                            IsConnectionClosed = true;

                            goto deleteFile;

                        default:
                            exception = new ObexException(
                                $"Got unexpected operation code during transfer: {packet.Opcode.ObexOperation}"
                            );
                            goto deleteFile;
                    }
                }
            }

            deleteFile:
            fileDeleted = true;
            DeleteTempFile(data.TempFileName);
            if (!string.IsNullOrEmpty(data.TempFileName))
            {
                data.TransferDone = true;
                data.Error = true;
                SendObexReceiveEvent(data);
            }

            finishTransfer:
            if (exception != null)
                throw exception;

            if (!fileDeleted && _destinationDirectory != "")
                MoveFile(data.TempFileName, _destinationDirectory, data.FileName);
        }

        private static void DeleteTempFile(string tempFile)
        {
            var file = tempFile;

            if (string.IsNullOrEmpty(file))
                return;

            if (File.Exists(file))
                File.Delete(file);
        }

        private static void MoveFile(string tempFile, string dir, string actualFile)
        {
            if (string.IsNullOrEmpty(tempFile))
                return;

            if (string.IsNullOrEmpty(actualFile))
                actualFile = "obex_file";

            actualFile = string.Concat(
                Path.GetFileNameWithoutExtension(actualFile),
                string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now),
                Path.GetExtension(actualFile)
            );

            var (src, dest) = (tempFile, Path.Join(dir, actualFile));

            var file = new FileInfo(src);
            file.MoveTo(dest);
        }

        private static (string FileName, int FileSize, bool IsFinal, byte[] buffer) GetPacketInfo(
            ObexPacket packet
        )
        {
            HeaderId headerId;
            var actualFileName = "";
            int fileSize = 0;

            if (packet.Opcode.IsFinalBitSet)
                headerId = HeaderId.EndOfBody;
            else
                headerId = HeaderId.Body;

            if (packet.Headers.TryGetValue(HeaderId.Name, out var nameHeader))
                actualFileName = nameHeader.GetValueAsUnicodeString(true);

            if (packet.Headers.TryGetValue(HeaderId.Length, out var lengthHeader))
                fileSize = lengthHeader.GetValueAsInt32();

            return (
                actualFileName,
                fileSize,
                packet.Opcode.IsFinalBitSet,
                packet.GetHeader(headerId).Buffer
            );
        }

        private void SendObexReceiveEvent(ReceiveTransferEventData eventData)
        {
            ReceiveTransferEventHandler?.Invoke(this, eventData);
        }
    }
}
