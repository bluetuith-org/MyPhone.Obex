﻿using System;
using System.Buffers.Binary;
using System.Threading.Tasks;
using GoodTimeStudio.MyPhone.OBEX.Headers;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX;

public class ObexConnectPacket : ObexPacket
{
    public static readonly uint ExtraFieldBits = sizeof(byte) * 2 + sizeof(ushort);

    private byte[] _extra = null!;

    public byte Flags;

    public ushort MaximumPacketLength = 0xFFFF;

    public byte ObexVersion = 0x10;

    /// <summary>
    ///     Create a empty instance ready for reading content from stream
    /// </summary>
    public ObexConnectPacket()
        : base(new ObexOpcode(ObexOperation.ServiceUnavailable, true)) { }

    public ObexConnectPacket(ObexServiceUuid targetService)
        : this(false, targetService) { }

    public ObexConnectPacket(bool disconnect, ObexServiceUuid targetService)
        : base(new ObexOpcode(disconnect ? ObexOperation.Disconnect : ObexOperation.Connect, true))
    {
        Headers[HeaderId.Target] = new ObexHeader(HeaderId.Target, targetService.Value);
    }

    public bool Disconnect { get; set; } = false;

    protected override byte[] GetExtraField()
    {
        if (_extra != null)
            return _extra;

        _extra = [ObexVersion, Flags, 0, 0];
        BinaryPrimitives.WriteUInt16BigEndian(_extra.AsSpan(2), MaximumPacketLength);

        return _extra;
    }

    protected override async Task<uint> ReadExtraField(DataReader reader)
    {
        var loaded = await reader.LoadAsync(ExtraFieldBits);
        if (loaded != ExtraFieldBits)
            throw new ObexException(
                "The underlying socket was closed before we were able to read the whole data."
            );
        ObexVersion = reader.ReadByte();
        Flags = reader.ReadByte();
        MaximumPacketLength = reader.ReadUInt16();
        return ExtraFieldBits;
    }
}
