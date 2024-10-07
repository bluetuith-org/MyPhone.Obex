﻿using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using System;
using System.Threading;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Opp
{
    public class BluetoothOppServerSession : BluetoothObexServerSession<OppServer>
    {
        public static readonly Guid OPP_ID = new Guid("00001105-0000-1000-8000-00805F9B34FB");

        private readonly string _sdir;

        public BluetoothOppServerSession(string destinationDirectory, CancellationTokenSource token) : base(OPP_ID, token)
        {
            _sdir = destinationDirectory;
        }

        protected override OppServer CreateObexServer(StreamSocket clientSocket, CancellationTokenSource token)
        {
            return new OppServer(clientSocket, _sdir, token);
        }
    }
}
