using System;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Networking.Sockets;
using static GoodTimeStudio.MyPhone.OBEX.Opp.OppServer;

namespace GoodTimeStudio.MyPhone.OBEX.Opp;

public partial class BluetoothOppServerSession : BluetoothObexServerSession<OppServer>
{
    public static readonly Guid OppId = new("00001105-0000-1000-8000-00805F9B34FB");

    protected readonly Func<ReceiveTransferEventData, bool> AuthFunc = delegate
    {
        return true;
    };

    protected readonly string DestinationDirectory = "";

    public BluetoothOppServerSession(
        CancellationTokenSource token,
        string destinationDirectory,
        Func<ReceiveTransferEventData, bool> authHandler
    )
        : base(OppId, token)
    {
        DestinationDirectory = destinationDirectory;
        AuthFunc = authHandler;
    }

    protected override OppServer CreateObexServer(
        StreamSocket clientSocket,
        CancellationTokenSource token
    )
    {
        return new OppServer(clientSocket, token, DestinationDirectory, AuthFunc);
    }
}
