using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class OppServer : ObexServer
    {
        private string _saveDirectory;

        private bool IsConnectionClosed = false;
        private StreamSocket _socket;

        public record ReceiveTransferEventData(
            string FileName,
            string TempFileName,
            long FileSize,
            long BytesTransferred,
            bool TransferDone
        );
        public EventHandler<ReceiveTransferEventData>? ReceiveTransferEventHandler;

        public OppServer(
            StreamSocket socket,
            string _sdir
          ) : base(socket.InputStream, socket.OutputStream, ObexServiceUuid.ObjectPush)
        {
            _socket = socket;
            _saveDirectory = _sdir;
        }

        public override async Task Run()
        {
            Exception? exception = null;

            if (IsConnectionClosed)
                return;

            listenForConnections:
            _cts.Token.ThrowIfCancellationRequested();

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

        createTemporaryFile:
            _cts.Token.ThrowIfCancellationRequested();

            var (tempFileName, actualFileName, actualFileSize, numBytes) = ("", "", 0, 0);

            using (var file = File.OpenWrite(Path.GetTempFileName()))
            {
                while (true)
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        packet.Opcode = new ObexOpcode(ObexOperation.Abort, true);
                        _writer.WriteBuffer(packet.ToBuffer());
                        await _writer.StoreAsync();

                        _socket.Dispose();
                        IsConnectionClosed = true;

                        goto deleteFile;
                    }

                    tempFileName = file.Name;
                    var sendPacket = new ObexPacket(new ObexOpcode(ObexOperation.Success, true));

                    packet = await ObexPacket.ReadFromStream(_reader);
                    switch (packet.Opcode.ObexOperation)
                    {
                        case ObexOperation.Put:
                            if (!packet.Opcode.IsFinalBitSet)
                                sendPacket = new ObexPacket(new ObexOpcode(ObexOperation.Continue, true));

                            _writer.WriteBuffer(sendPacket.ToBuffer());
                            await _writer.StoreAsync();

                            var packetInfo = GetPacketInfo(packet);
                            var (pFileName, pFileSize, pBufferLength) = (
                                packetInfo.FileName, packetInfo.FileSize, packetInfo.buffer.Length
                            );
                            if (pFileName != "")
                                actualFileName = packetInfo.FileName;
                            if (pFileSize > 0)
                                actualFileSize = packetInfo.FileSize;
                            if (pBufferLength > 0)
                                numBytes += packetInfo.buffer.Length;

                            file.Write(packetInfo.buffer);
                            SendObexReceiveEvent(new ReceiveTransferEventData(
                                actualFileName,
                                tempFileName,
                                actualFileSize,
                                numBytes,
                                packetInfo.IsFinal
                            ));

                            if (packetInfo.IsFinal)
                                goto moveFile;

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

        moveFile:
            MoveFile(tempFileName, actualFileName);
            goto createTemporaryFile;

        deleteFile:
            DeleteTempFile(tempFileName);
            if (exception != null)
                throw exception;
        }

        private void MoveFile(string tempFile, string actualFile)
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

            var (src, dest) = (tempFile, Path.Join(_saveDirectory, actualFile));
            Task.Run(() =>
            {
                try
                {
                    var file = new FileInfo(tempFile);
                    file.MoveTo(dest);
                }
                catch { }
            });
        }

        private void DeleteTempFile(string tempFile)
        {
            var file = tempFile;

            Task.Run(() =>
            {
                if (string.IsNullOrEmpty(file))
                    return;

                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch { }
            });
        }

        private static (string FileName, int FileSize, bool IsFinal, byte[] buffer) GetPacketInfo(ObexPacket packet)
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
