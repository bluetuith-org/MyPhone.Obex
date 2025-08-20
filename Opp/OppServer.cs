using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp;

public class OppServer : ObexServer
{
    private readonly StreamSocket _socket;

    private readonly Func<ReceiveTransferEventData, bool> _authFunc = delegate
    {
        return true;
    };

    private readonly string _destinationDirectory = "";

    public EventHandler<ReceiveTransferEventData> ReceiveTransferEventHandler;

    public OppServer(
        StreamSocket socket,
        CancellationTokenSource token,
        string directory,
        Func<ReceiveTransferEventData, bool> authHandler
    )
        : base(socket.InputStream, socket.OutputStream, ObexServiceUuid.ObjectPush, token)
    {
        if (
            !string.IsNullOrEmpty(_destinationDirectory) && !Directory.Exists(_destinationDirectory)
        )
            throw new DirectoryNotFoundException(_destinationDirectory);

        _socket = socket;
        Cts = token;
        _destinationDirectory = directory;
        _authFunc = authHandler;
    }

    public override void CancelTransfer()
    {
        Cts.Cancel();
    }

    public override async Task Run()
    {
        listenForConnections:
        Cts.Token.ThrowIfCancellationRequested();

        ObexPacket packet = await ObexPacket.ReadFromStream<ObexConnectPacket>(Reader);
        switch (packet.Opcode.ObexOperation)
        {
            case ObexOperation.Connect:
                packet.Opcode = new ObexOpcode(ObexOperation.Success, true);
                packet.WriteToStream(Writer);
                await Writer.StoreAsync();
                break;
            default:
                packet = new ObexPacket(new ObexOpcode(ObexOperation.ServiceUnavailable, true));
                packet.WriteToStream(Writer);
                goto listenForConnections;
        }

        Cts.Token.ThrowIfCancellationRequested();

        ReceiveTransferEventData data = new();

        FileStream file = null;
        var filename = "";
        var tempFileName = "";

        var firstPut = true;
        var finalPut = false;
        var aborted = false;
        var disconnected = false;

        var exception = false;

        try
        {
            while (!disconnected && !aborted && !Cts.Token.IsCancellationRequested)
            {
                if (finalPut && file != null)
                {
                    var src = file.Name;
                    await file.DisposeAsync();

                    MoveFile(src, data.FilePath);

                    data.TransferDone = true;
                    SendObexReceiveEvent(data);

                    file = null!;
                }

                file ??= File.OpenWrite(Path.GetTempFileName());
                tempFileName = file.Name;

                var sendPacket = new ObexPacket(new ObexOpcode(ObexOperation.Success, true));

                packet = await ObexPacket.ReadFromStream(Reader);
                switch (packet.Opcode.ObexOperation)
                {
                    case ObexOperation.Put:
                        if (finalPut)
                            data = new ReceiveTransferEventData();

                        if (data.HostName == "")
                            data.HostName = _socket.Information.RemoteHostName.RawName;

                        var (fileName, fileSize, isFinal, buffer) = GetPacketInfo(packet, firstPut);
                        if (fileSize > 0)
                            data.FileSize = fileSize;
                        if (!string.IsNullOrEmpty(fileName))
                            filename = fileName;
                        if (string.IsNullOrEmpty(data.FilePath) && !string.IsNullOrEmpty(filename))
                            data.FilePath = string.IsNullOrEmpty(_destinationDirectory)
                                ? Path.Combine(Path.GetDirectoryName(file.Name)!, filename)
                                : Path.Combine(_destinationDirectory, filename);

                        if (firstPut)
                        {
                            SendObexReceiveEvent(data with { FileName = filename, Queued = true });
                            if (!_authFunc(data))
                            {
                                CancelTransfer();
                                return;
                            }
                        }

                        firstPut = false;
                        finalPut = isFinal;
                        if (!packet.Opcode.IsFinalBitSet)
                            sendPacket = new ObexPacket(
                                new ObexOpcode(ObexOperation.Continue, true)
                            );

                        file.Write(buffer);

                        sendPacket.WriteToStream(Writer);
                        await Writer.StoreAsync();
                        data.BytesTransferred += buffer.Length;

                        if (finalPut)
                        {
                            data.TransferDone = true;
                            continue;
                        }

                        SendObexReceiveEvent(data);

                        continue;

                    case ObexOperation.Abort:
                    case ObexOperation.Disconnect:
                        disconnected = packet.Opcode.ObexOperation == ObexOperation.Disconnect;
                        aborted = packet.Opcode.ObexOperation == ObexOperation.Abort;

                        sendPacket.WriteToStream(Writer);
                        await Writer.StoreAsync();

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
            if ((!disconnected && exception) || Cts.IsCancellationRequested || aborted)
            {
                data.Error = true;
                data.TransferDone = false;
                SendObexReceiveEvent(data);
            }

            try
            {
                if (Cts.IsCancellationRequested && !exception)
                {
                    packet.Opcode = new ObexOpcode(ObexOperation.Abort, true);
                    packet.WriteToStream(Writer);
                    await Writer.StoreAsync();
                }

                _socket?.Dispose();
                file?.Dispose();
                DeleteTempFile(tempFileName);
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

        new FileInfo(src).MoveTo(dest);
    }

    private static (string FileName, int FileSize, bool IsFinal, byte[] buffer) GetPacketInfo(
        ObexPacket packet,
        bool isFirstPut
    )
    {
        HeaderId headerId;
        var actualFileName = "";
        var fileSize = 0;

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
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}",
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

    public record ReceiveTransferEventData
    {
        public long BytesTransferred;
        public bool Error;
        public string FileName = "";
        public string FilePath = "";
        public long FileSize;
        public string HostName = "";
        public bool Queued;
        public bool SessionClosed;
        public bool TransferDone;
    }
}
