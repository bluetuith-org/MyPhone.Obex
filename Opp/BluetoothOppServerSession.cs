using System;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Networking.Sockets;
using static GoodTimeStudio.MyPhone.OBEX.Opp.OppServer;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public partial class BluetoothOppServerSession : BluetoothObexServerSession<OppServer>
    {
        public static readonly Guid OPP_ID = new Guid("00001105-0000-1000-8000-00805F9B34FB");

        protected readonly string _destinationDirectory = "";
        protected readonly Func<ReceiveTransferEventData, bool> _authFunc = delegate
        {
            return true;
        };

        public BluetoothOppServerSession(
            CancellationTokenSource token,
            string destinationDirectory,
            Func<ReceiveTransferEventData, bool> authHandler
        )
            : base(OPP_ID, token)
        {
            _destinationDirectory = destinationDirectory;
            _authFunc = authHandler;
        }

        protected override OppServer CreateObexServer(
            StreamSocket clientSocket,
            CancellationTokenSource token
        )
        {
            return new OppServer(clientSocket, token, _destinationDirectory, _authFunc);
        }
    }
}
