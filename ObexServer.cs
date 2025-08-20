using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX;

public class ObexServer
{
    protected CancellationTokenSource Cts;
    protected readonly DataReader Reader;

    private readonly ObexServiceUuid _serviceUuid;
    protected readonly DataWriter Writer;

    public ObexServer(
        IInputStream inputStream,
        IOutputStream outputStream,
        ObexServiceUuid serviceUuid,
        CancellationTokenSource token
    )
    {
        Reader = new DataReader(inputStream);
        Writer = new DataWriter(outputStream);
        _serviceUuid = serviceUuid;
        Cts = token;
    }

    public virtual async Task Run()
    {
        while (true)
        {
            Cts.Token.ThrowIfCancellationRequested();

            ObexPacket packet = await ObexPacket.ReadFromStream<ObexConnectPacket>(Reader);
            if (packet.Opcode.ObexOperation == ObexOperation.Connect)
                if (packet.Headers.TryGetValue(HeaderId.Target, out var header))
                    if (header.Buffer.ToArray().SequenceEqual(_serviceUuid.Value))
                    {
                        packet.Opcode = new ObexOpcode(ObexOperation.Success, true);
                        packet.WriteToStream(Writer);
                        await Writer.StoreAsync();
                        break;
                    }

            packet = new ObexPacket(new ObexOpcode(ObexOperation.ServiceUnavailable, true));
            packet.WriteToStream(Writer);
        }

        while (true)
        {
            Cts.Token.ThrowIfCancellationRequested();

            var packet = await ObexPacket.ReadFromStream(Reader);

            var response = OnClientRequest(packet);
            if (response != null)
            {
                response.WriteToStream(Writer);
            }
            else
            {
                Writer.WriteByte(0xC6); // Not Acceptable
                Writer.WriteUInt16(3);
            }

            await Writer.StoreAsync();
        }
    }

    public void StopServer()
    {
        Cts.Cancel();
    }

    public virtual void CancelTransfer() { }

    /// <summary>
    ///     Handle client request.
    /// </summary>
    /// <remarks>
    ///     This method will be called whenever a client request arrived.
    /// </remarks>
    /// <param name="clientRequestPacket"></param>
    /// <returns>
    ///     The OBEX response packet. If the server doesn't know how to handle this client request, return null.
    /// </returns>
    protected virtual ObexPacket? OnClientRequest(ObexPacket clientRequestPacket)
    {
        return null;
    }
}
