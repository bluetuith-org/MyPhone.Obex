using System;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Devices.Bluetooth;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp;

public partial class BluetoothOppClientSession : BluetoothObexClientSession<OppClient>
{
    public static readonly Guid OppId = new("00001105-0000-1000-8000-00805F9B34FB");

    public BluetoothOppClientSession(BluetoothDevice bluetoothDevice, CancellationTokenSource token)
        : base(bluetoothDevice, OppId, ObexServiceUuid.ObjectPush, token) { }

    protected override bool CheckFeaturesRequirementBySdpRecords()
    {
        return true;
    }

    public override OppClient CreateObexClient(StreamSocket socket, CancellationTokenSource token)
    {
        return new OppClient(socket.InputStream, socket.OutputStream, token);
    }
}
