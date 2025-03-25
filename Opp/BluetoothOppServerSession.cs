using System;
using System.IO;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class BluetoothOppServerSession : BluetoothObexServerSession<OppServer>
    {
        public static readonly Guid OPP_ID = new Guid("00001105-0000-1000-8000-00805F9B34FB");

        protected readonly string _destinationDirectory = "";

        public BluetoothOppServerSession(CancellationTokenSource token, string destinationDirectory)
            : base(OPP_ID, token)
        {
            _destinationDirectory = destinationDirectory;
        }

        protected override OppServer CreateObexServer(
            StreamSocket clientSocket,
            CancellationTokenSource token
        )
        {
            return new OppServer(clientSocket, token, _destinationDirectory);
        }
    }
}
