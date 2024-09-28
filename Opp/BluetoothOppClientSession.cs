using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using System;
using Windows.Devices.Bluetooth;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class BluetoothOppClientSession : BluetoothObexClientSession<OppClient>
    {
        public static readonly Guid OPP_ID = new Guid("00001105-0000-1000-8000-00805F9B34FB");

        public BluetoothOppClientSession(BluetoothDevice bluetoothDevice) : base(bluetoothDevice, OPP_ID, ObexServiceUuid.ObjectPush)
        {
        }

        protected override bool CheckFeaturesRequirementBySdpRecords()
        {
            return true;
        }

        public override OppClient CreateObexClient(StreamSocket socket)
        {
            return new OppClient(socket);
        }
    }
}

