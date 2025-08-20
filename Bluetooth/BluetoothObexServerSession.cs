using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Bluetooth;

public class BluetoothObexServerSessionClientEventArgs<T>
    where T : ObexServer
{
    public BluetoothObexServerSessionClientEventArgs(
        BluetoothClientInformation clientInformation,
        T obexServer
    )
    {
        ClientInfo =
            clientInformation ?? throw new ArgumentNullException(nameof(clientInformation));
        ObexServer = obexServer ?? throw new ArgumentNullException(nameof(obexServer));
    }

    public BluetoothClientInformation ClientInfo { get; set; }

    public T ObexServer { get; set; }
}

public class BluetoothObexServerSessionClientAcceptedEventArgs<T>
    : BluetoothObexServerSessionClientEventArgs<T>
    where T : ObexServer
{
    public BluetoothObexServerSessionClientAcceptedEventArgs(
        BluetoothClientInformation clientInformation,
        T obexServer
    )
        : base(clientInformation, obexServer) { }
}

public class BluetoothObexServerSessionClientDisconnectedEventArgs<T>
    : BluetoothObexServerSessionClientEventArgs<T>
    where T : ObexServer
{
    public BluetoothObexServerSessionClientDisconnectedEventArgs(
        BluetoothClientInformation clientInformation,
        T obexServer,
        ObexException obexException
    )
        : base(clientInformation, obexServer)
    {
        ObexServerException = obexException;
    }

    /// <summary>
    ///     The <see cref="ObexException" /> that causes the client disconnected.
    /// </summary>
    public ObexException ObexServerException { get; }
}

public abstract class BluetoothObexServerSession<T> : IDisposable
    where T : ObexServer
{
    public delegate void BluetoothObexServerSessionClientAcceptedEventHandler(
        BluetoothObexServerSession<T> sender,
        BluetoothObexServerSessionClientAcceptedEventArgs<T> e
    );

    public delegate void BluetoothObexServerSessionClientDisconnectedEventHandler(
        BluetoothObexServerSession<T> sender,
        BluetoothObexServerSessionClientDisconnectedEventArgs<T> args
    );

    private readonly Dictionary<BluetoothClientInformation, T> _connections;
    private readonly CancellationTokenSource _cts;
    private readonly uint _maxConnections;
    private RfcommServiceProvider? _serviceProvider;

    private StreamSocketListener? _socketListener;

    public BluetoothObexServerSession(Guid serviceUuid, CancellationTokenSource token)
        : this(serviceUuid, 0, token) { }

    /// <summary>
    ///     Initialize BluetoothObexServerSession
    /// </summary>
    /// <param name="serviceUuid">The bluetooth service UUID</param>
    /// <param name="maxConnection">The maximum number of connections allowed. 0 means no limits.</param>
    public BluetoothObexServerSession(
        Guid serviceUuid,
        uint maxConnections,
        CancellationTokenSource token
    )
    {
        ServiceUuid = serviceUuid;
        _connections = new Dictionary<BluetoothClientInformation, T>();
        _maxConnections = maxConnections;
        _cts = token;
    }

    public Guid ServiceUuid { get; set; }

    public bool ServerStarted => _socketListener != null;

    public void Dispose()
    {
        _cts.Cancel();
        _serviceProvider?.StopAdvertising();
        foreach (var obexServer in _connections.Values)
            obexServer.StopServer();
        _socketListener?.Dispose();
    }

    public event BluetoothObexServerSessionClientAcceptedEventHandler? ClientAccepted;
    public event BluetoothObexServerSessionClientDisconnectedEventHandler? ClientDisconnected;

    public async Task StartServerAsync()
    {
        if (ServerStarted)
            throw new InvalidOperationException(
                "The BluetoothObexServerSession is already started."
            );

        StreamSocketListener socketListener;
        try
        {
            _cts.Token.ThrowIfCancellationRequested();

            socketListener = new StreamSocketListener();
            socketListener.ConnectionReceived += SocketListener_ConnectionReceived;

            _serviceProvider = await RfcommServiceProvider.CreateAsync(
                RfcommServiceId.FromUuid(ServiceUuid)
            );
            await socketListener.BindServiceNameAsync(
                _serviceProvider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionWithAuthentication
            );
            _serviceProvider.StartAdvertising(socketListener);
        }
        catch (Exception ex)
        {
            var socketErrorStatus = SocketError.GetStatus(ex.HResult);
            if (socketErrorStatus != SocketErrorStatus.Unknown)
                throw new BluetoothObexSessionException(
                    "Unable to bind and start the OBEX server on Bluetooth socket",
                    socketError: socketErrorStatus
                );

            throw;
        }

        _socketListener = socketListener;
    }

    private void SocketListener_ConnectionReceived(
        StreamSocketListener sender,
        StreamSocketListenerConnectionReceivedEventArgs args
    )
    {
        if (_maxConnections > 0 && _connections.Count >= _maxConnections)
            return;
        var obexServer = CreateObexServer(
            args.Socket,
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token)
        );
        BluetoothClientInformation clientInformation = new(
            args.Socket.Information.RemoteAddress,
            args.Socket.Information.RemoteServiceName
        );
        _connections[clientInformation] = obexServer;
        ClientAccepted?.Invoke(
            this,
            new BluetoothObexServerSessionClientAcceptedEventArgs<T>(clientInformation, obexServer)
        );
        Task.Run(async () => await RunObexServer(obexServer, clientInformation));
    }

    private async Task RunObexServer(T obexSerber, BluetoothClientInformation clientInformation)
    {
        ObexException exception = null;

        try
        {
            await obexSerber.Run();
        }
        catch (ObexException ex)
        {
            exception = ex;
            obexSerber.StopServer();
        }
        finally
        {
            ClientDisconnected?.Invoke(
                this,
                new BluetoothObexServerSessionClientDisconnectedEventArgs<T>(
                    clientInformation,
                    obexSerber,
                    exception
                )
            );

            _connections.Remove(clientInformation);
        }
    }

    protected abstract T CreateObexServer(StreamSocket clientSocket, CancellationTokenSource token);
}
