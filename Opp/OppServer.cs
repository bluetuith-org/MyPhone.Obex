using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class OppServer : ObexServer
    {
        private readonly StreamSocket _socket;
        private readonly CancellationTokenSource _transfercts;

        private readonly string destinationDirectory = "";
        private readonly Func<ReceiveTransferEventData, bool> authFunc = delegate
        {
            return true;
        };

        public record class ReceiveTransferEventData
        {
            public string HostName = "";
            public string FileName = "";
            public string FilePath = "";
            public string TempFileName = "";
            public long FileSize;
            public long BytesTransferred;
            public bool Queued;
            public bool TransferDone;
            public bool Error;
            public bool SessionClosed;
        };

        public EventHandler<ReceiveTransferEventData> ReceiveTransferEventHandler;

        public OppServer(
            StreamSocket socket,
            CancellationTokenSource token,
            string directory,
            Func<ReceiveTransferEventData, bool> authHandler
        )
            : base(socket.InputStream, socket.OutputStream, ObexServiceUuid.ObjectPush, token)
        {
            _socket = socket;
            _transfercts = token;
            destinationDirectory = directory;
            authFunc = authHandler;
        }

        public override void CancelTransfer()
        {
            _transfercts.Cancel();
        }

        public override async Task Run()
        {
            listenForConnections:
            _transfercts.Token.ThrowIfCancellationRequested();

            ObexPacket packet = await ObexPacket.ReadFromStream<ObexConnectPacket>(_reader);
            switch (packet.Opcode.ObexOperation)
            {
                case ObexOperation.Connect:
                    packet.Opcode = new ObexOpcode(ObexOperation.Success, true);
                    packet.WriteToStream(_writer);
                    await _writer.StoreAsync();
                    break;
                default:
                    packet = new ObexPacket(new ObexOpcode(ObexOperation.ServiceUnavailable, true));
                    packet.WriteToStream(_writer);
                    goto listenForConnections;
            }

            _transfercts.Token.ThrowIfCancellationRequested();

            ReceiveTransferEventData data = new();

            FileStream file = null;
            var filename = "";

            var firstPut = true;
            var finalPut = false;
            var aborted = false;
            var disconnected = false;

            var exception = false;

            try
            {
                while (!disconnected || aborted || !_transfercts.Token.IsCancellationRequested)
                {
                    if (finalPut && file != null)
                    {
                        var src = file.Name;
                        file.Dispose();

                        MoveFile(src, data.FilePath);
                        file = null;
                    }

                    var sendPacket = new ObexPacket(new ObexOpcode(ObexOperation.Success, true));

                    packet = await ObexPacket.ReadFromStream(_reader);
                    switch (packet.Opcode.ObexOperation)
                    {
                        case ObexOperation.Put:
                            if (finalPut)
                                data = new();

                            if (data.HostName == "")
                                data.HostName = _socket.Information.RemoteHostName.RawName;

                            var (FileName, FileSize, IsFinal, buffer) = GetPacketInfo(
                                packet,
                                firstPut
                            );
                            data.FileName = FileName;
                            if (FileSize > 0)
                                data.FileSize = FileSize;
                            if (FileName != "")
                                filename = FileName;

                            if (firstPut)
                            {
                                SendObexReceiveEvent(data with { Queued = true });
                                if (!authFunc(data))
                                {
                                    CancelTransfer();
                                    return;
                                }
                            }

                            firstPut = false;
                            finalPut = IsFinal;
                            if (!packet.Opcode.IsFinalBitSet)
                                sendPacket = new ObexPacket(
                                    new ObexOpcode(ObexOperation.Continue, true)
                                );

                            file ??= File.OpenWrite(Path.GetTempFileName());
                            file.Write(buffer);

                            sendPacket.WriteToStream(_writer);
                            await _writer.StoreAsync();

                            data.BytesTransferred += buffer.Length;
                            if (!string.IsNullOrEmpty(data.TempFileName))
                                data.TempFileName = file.Name;

                            if (finalPut)
                            {
                                data.TransferDone = true;
                                data.FilePath =
                                    (
                                        string.IsNullOrEmpty(destinationDirectory)
                                        || string.IsNullOrWhiteSpace(destinationDirectory)
                                        || string.IsNullOrEmpty(filename)
                                    )
                                        ? data.TempFileName
                                        : Path.Combine(
                                            Path.GetDirectoryName(destinationDirectory)!,
                                            filename
                                        );
                            }

                            SendObexReceiveEvent(data);

                            continue;

                        case ObexOperation.Abort:
                        case ObexOperation.Disconnect:
                            disconnected = packet.Opcode.ObexOperation == ObexOperation.Disconnect;
                            aborted = packet.Opcode.ObexOperation == ObexOperation.Abort;

                            sendPacket.WriteToStream(_writer);
                            await _writer.StoreAsync();

                            return;

                        default:
                            throw new ObexException(
                                $"Got unexpected operation code during transfer: {packet.Opcode.ObexOperation}"
                            );
                    }
                }
            }
            catch (Exception ex)
            {
                exception = true;
                throw new ObexException(ex.Message);
            }
            finally
            {
                data.SessionClosed = true;
                if ((!disconnected && exception) || _transfercts.IsCancellationRequested || aborted)
                {
                    data.Error = true;
                    data.TransferDone = false;
                }

                SendObexReceiveEvent(data);

                try
                {
                    if (_transfercts.IsCancellationRequested && !exception)
                    {
                        DeleteTempFile(data.TempFileName);

                        packet.Opcode = new ObexOpcode(ObexOperation.Abort, true);
                        packet.WriteToStream(_writer);
                        await _writer.StoreAsync();
                    }

                    _socket?.Dispose();
                    file?.Dispose();
                }
                catch { }
            }
        }

        private static void DeleteTempFile(string file)
        {
            if (string.IsNullOrEmpty(file))
                return;

            if (File.Exists(file))
                File.Delete(file);
        }

        private static void MoveFile(string src, string dest)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dest) || src == dest)
                return;

            try
            {
                new FileInfo(src).MoveTo(dest);
            }
            catch { }
        }

        private static (string FileName, int FileSize, bool IsFinal, byte[] buffer) GetPacketInfo(
            ObexPacket packet,
            bool isFirstPut
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

            if (isFirstPut)
            {
                if (string.IsNullOrEmpty(actualFileName) || fileSize <= 0)
                    throw new Exception(
                        "Invalid first packet from remote device. Filename is empty and/or file size is zero."
                    );

                actualFileName = string.Concat(
                    Path.GetFileNameWithoutExtension(actualFileName),
                    string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now),
                    Path.GetExtension(actualFileName)
                );
            }

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
