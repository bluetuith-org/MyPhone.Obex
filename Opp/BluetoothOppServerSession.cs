using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class BluetoothOppServerSession: BluetoothObexServerSession<OppServer>
    {
        public static readonly Guid OPP_ID = new Guid("00001105-0000-1000-8000-00805F9B34FB");

        private readonly string _sdir;

        public BluetoothOppServerSession(string destinationDirectory): base(OPP_ID)
        {
            _sdir = destinationDirectory;
        }

        protected override OppServer CreateObexServer(StreamSocket clientSocket)
        {
            return new OppServer(clientSocket, _sdir);
        }
    }
}
