using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using System;
using System.Threading;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class BluetoothOppServerSession : BluetoothObexServerSession<OppServer>
    {
        public static readonly Guid OPP_ID = new Guid("00001105-0000-1000-8000-00805F9B34FB");

        public BluetoothOppServerSession(CancellationTokenSource token) : base(OPP_ID, token)
        {
        }

        protected override OppServer CreateObexServer(StreamSocket clientSocket, CancellationTokenSource token)
        {
            return new OppServer(clientSocket, token);
        }
    }
}
