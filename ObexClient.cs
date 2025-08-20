using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX;

public class ObexClient
{
    protected CancellationTokenSource CancellationTokenSource;
    protected readonly DataReader Reader;
    protected readonly DataWriter Writer;

    public ObexClient(
        IInputStream inputStream,
        IOutputStream outputStream,
        CancellationTokenSource token
    )
    {
        Reader = new DataReader(inputStream);
        Writer = new DataWriter(outputStream);
        CancellationTokenSource = token;
    }

    public bool Conntected { get; private set; }

    /// <summary>
    ///     Send OBEX Connect packet to the server.
    /// </summary>
    /// <param name="targetService">A 16-length byte array indicates the UUID of the target service.</param>
    /// <exception cref="InvalidOperationException">The Connect method can call only once and it is already called before.</exception>
    /// <exception cref="ObexException">
    ///     The request failed due to an underlying issue such as connection issue, or the server
    ///     reply with a invalid response
    /// </exception>
    public async Task ConnectAsync(ObexServiceUuid targetService)
    {
        if (Conntected)
            throw new InvalidOperationException("ObexClient is already connected to a ObexServer");

        var packet = new ObexConnectPacket(targetService);
        packet.WriteToStream(Writer);
        await Writer.StoreAsync();

        var response = await ObexPacket.ReadFromStream<ObexConnectPacket>(Reader);

        if (response.Opcode.ObexOperation != ObexOperation.Success)
            throw new ObexRequestException(
                response.Opcode,
                "Unable to connect to the target OBEX service."
            );

        Conntected = true;
        OnConnected(response);
    }

    protected virtual void OnConnected(ObexPacket connectionResponse) { }

    public async Task DisconnectAsync()
    {
        if (!Conntected)
            throw new InvalidOperationException("ObexClient is not connected to any ObexServer");
    }

    protected async Task AbortAsync()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
            await CancellationTokenSource.CancelAsync();

        var request = new ObexPacket(new ObexOpcode(ObexOperation.Abort, true));
        request.WriteToStream(Writer);
        await Writer?.StoreAsync();
    }

    /// <summary>
    ///     Send OBEX request to MSE
    /// </summary>
    /// <param name="req">The request packet</param>
    /// <returns>
    ///     Response packet. The resposne packet is null if the MSE did not send back any response, or the response is
    ///     corrupted
    /// </returns>
    /// <exception cref="ObexRequestException">Throws if get an valid response, but its opcode is unsuccessful</exception>
    /// <exception cref="ObexException"> due to an underlying issue such as connection loss, invalid server response</exception>
    public async Task<ObexPacket> RunObexRequestAsync(ObexPacket req)
    {
        if (!Conntected)
            throw new InvalidOperationException("ObexClient is not connected to any ObexServer");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();

        var requestOperation = req.Opcode.ObexOperation;
        if (requestOperation == null)
            throw new InvalidOperationException("User-defined opcode is not supported");

        ObexPacket? response = null;

        using var bodyMemoryStream = new MemoryStream();
        do
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                await AbortAsync();
                return null;
            }

            req.WriteToStream(Writer);
            await Writer.StoreAsync();

            ObexPacket subResponse;
            subResponse = await ObexPacket.ReadFromStream(Reader);

            if (response == null)
                response = subResponse;

            switch (subResponse.Opcode.ObexOperation)
            {
                case ObexOperation.Success:
                    if (
                        subResponse.Headers.TryGetValue(HeaderId.EndOfBody, out var endOfBodyHeader)
                    )
                        bodyMemoryStream.Write(endOfBodyHeader.Buffer);
                    response.Opcode = subResponse.Opcode;
                    response.BodyBuffer = bodyMemoryStream.ToArray();
                    return response;
                case ObexOperation.Continue:
                    if (subResponse.Headers.TryGetValue(HeaderId.Body, out var bodyHeader))
                        bodyMemoryStream.Write(bodyHeader.Buffer);
                    break;
                default:
                    throw new ObexRequestException(
                        subResponse.Opcode,
                        $"The {requestOperation} request failed with opcode {subResponse.Opcode}"
                    );
            }

            req = new ObexPacket(new ObexOpcode(requestOperation.Value, true));
        } while (true);
    }
}
