using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using GoodTimeStudio.MyPhone.OBEX.Bluetooth;
using Windows.Devices.Bluetooth;
using Windows.Networking.Sockets;

namespace GoodTimeStudio.MyPhone.OBEX.Map;

public partial class BluetoothMasClientSession : BluetoothObexClientSession<MasClient>
{
    public static readonly Guid MapId = new("00001132-0000-1000-8000-00805f9b34fb");

    public BluetoothMasClientSession(BluetoothDevice bluetoothDevice, CancellationTokenSource token)
        : base(bluetoothDevice, MapId, ObexServiceUuid.MessageAccess, token) { }

    public Version? ProfileVersion { get; private set; }

    public MapSupportedFeatures SupportedFeatures { get; private set; }

    protected override bool CheckFeaturesRequirementBySdpRecords()
    {
        Debug.Assert(SdpRecords != null);

        {
            if (
                SdpRecords.TryGetValue(0x9, out var rawAttributeValue)
                && rawAttributeValue is { Count: >= 10 }
            )
                ProfileVersion = new Version(
                    rawAttributeValue.ElementAt(8),
                    rawAttributeValue.ElementAt(9)
                );
            else
                return false;
        }

        {
            if (
                SdpRecords.TryGetValue(0x317, out var rawAttributeValue)
                && rawAttributeValue != null
            )
                SupportedFeatures = (MapSupportedFeatures)
                    BinaryPrimitives.ReadInt32BigEndian(
                        new ReadOnlySpan<byte>(rawAttributeValue.Skip(1).ToArray())
                    );
            else
                // For compatibility
                SupportedFeatures = (MapSupportedFeatures)0x1F;
        }

        return true;
    }

    public override MasClient CreateObexClient(StreamSocket socket, CancellationTokenSource token)
    {
        return new MasClient(socket, token);
    }
}
