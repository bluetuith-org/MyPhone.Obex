using System;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Map
{
    public class BluetoothMnsServerSession : BluetoothObexServerSession<MnsServer>
    {
        public static readonly Guid MAP_MNS_Id = new Guid("00001133-0000-1000-8000-00805f9b34fb");

        public BluetoothMnsServerSession(CancellationTokenSource token)
            : base(MAP_MNS_Id, 1, token) { }

        protected override MnsServer CreateObexServer(
            StreamSocket clientSocket,
            CancellationTokenSource token
        )
        {
            return new MnsServer(clientSocket.InputStream, clientSocket.OutputStream, token);
        }
    }
}
