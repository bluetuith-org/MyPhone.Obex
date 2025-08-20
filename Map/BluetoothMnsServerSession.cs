using System;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Map;

public partial class BluetoothMnsServerSession : BluetoothObexServerSession<MnsServer>
{
    public static readonly Guid MapMnsId = new("00001133-0000-1000-8000-00805f9b34fb");

    public BluetoothMnsServerSession(CancellationTokenSource token)
        : base(MapMnsId, 1, token) { }

    protected override MnsServer CreateObexServer(
        StreamSocket clientSocket,
        CancellationTokenSource token
    )
    {
        return new MnsServer(clientSocket.InputStream, clientSocket.OutputStream, token);
    }
}
