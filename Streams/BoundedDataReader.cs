using System;
using Windows.Storage.Streams;

namespace GoodTimeStudio.MyPhone.OBEX.Streams;

/// <summary>
///     A data reader with a limit on the number of bytes that can be read.
///     A <see cref="DataReaderQuotaExceedException" /> will be thrown if the number of bytes a operation
///     try to read exceed the limit.
/// </summary>
public partial class BoundedDataReader(IDataReader actualReader, uint readQuota) : IDataReader
{
    public uint RemainingQuota = readQuota;

    public byte ReadByte()
    {
        ConsumeBytes(sizeof(byte));
        return actualReader.ReadByte();
    }

    public void ReadBytes(byte[] value)
    {
        ConsumeBytes((uint)value.Length);
        actualReader.ReadBytes(value);
    }

    public IBuffer ReadBuffer(uint length)
    {
        ConsumeBytes(length);
        return actualReader.ReadBuffer(length);
    }

    public bool ReadBoolean()
    {
        ConsumeBytes(sizeof(bool));
        return actualReader.ReadBoolean();
    }

    public Guid ReadGuid()
    {
        // TODO: what is the size of Guid
        throw new NotImplementedException();
    }

    public short ReadInt16()
    {
        ConsumeBytes(sizeof(short));
        return actualReader.ReadInt16();
    }

    public int ReadInt32()
    {
        ConsumeBytes(sizeof(int));
        return actualReader.ReadInt32();
    }

    public long ReadInt64()
    {
        ConsumeBytes(sizeof(long));
        return actualReader.ReadInt64();
    }

    public ushort ReadUInt16()
    {
        ConsumeBytes(sizeof(ushort));
        return actualReader.ReadUInt16();
    }

    public uint ReadUInt32()
    {
        ConsumeBytes(sizeof(uint));
        return actualReader.ReadUInt32();
    }

    public ulong ReadUInt64()
    {
        ConsumeBytes(sizeof(ulong));
        return actualReader.ReadUInt64();
    }

    public float ReadSingle()
    {
        ConsumeBytes(sizeof(float));
        return actualReader.ReadSingle();
    }

    public double ReadDouble()
    {
        ConsumeBytes(sizeof(double));
        return actualReader.ReadDouble();
    }

    public string ReadString(uint codeUnitCount)
    {
        ConsumeBytes(codeUnitCount);
        return actualReader.ReadString(codeUnitCount);
    }

    public DateTimeOffset ReadDateTime()
    {
        throw new NotImplementedException();
    }

    public TimeSpan ReadTimeSpan()
    {
        throw new NotImplementedException();
    }

    public DataReaderLoadOperation LoadAsync(uint count)
    {
        throw new InvalidOperationException("Now allowed");
    }

    public IBuffer DetachBuffer()
    {
        return actualReader.DetachBuffer();
    }

    public IInputStream DetachStream()
    {
        throw new InvalidOperationException("Now allowed");
    }

    public ByteOrder ByteOrder
    {
        get => actualReader.ByteOrder;
        set => actualReader.ByteOrder = value;
    }

    public InputStreamOptions InputStreamOptions
    {
        get => actualReader.InputStreamOptions;
        set => throw new InvalidOperationException("Now allowed");
    }

    public uint UnconsumedBufferLength => actualReader.UnconsumedBufferLength;

    public UnicodeEncoding UnicodeEncoding
    {
        get => actualReader.UnicodeEncoding;
        set => actualReader.UnicodeEncoding = value;
    }

    private void ConsumeBytes(uint numberOfBytes)
    {
        if (RemainingQuota < numberOfBytes)
            throw new BoundedDataReaderQuotaExceedException(
                RemainingQuota + numberOfBytes,
                numberOfBytes
            );
        RemainingQuota -= numberOfBytes;
    }
}

public class BoundedDataReaderQuotaExceedException(
    uint remainingQuota,
    uint numberOfBytesAttemptToRead
)
    : Exception(
        $"Attempt to read {numberOfBytesAttemptToRead} bytes with BoundedDataReader, exceeding the remaining quota ${remainingQuota} bytes."
    )
{
    public uint RemainingQuota { get; } = remainingQuota;
    public uint NumberOfBytesAttemptToRead { get; } = numberOfBytesAttemptToRead;
}
