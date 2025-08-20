﻿using System;

namespace GoodTimeStudio.MyPhone.OBEX;

public sealed class ObexOpcode
{
    public ObexOpcode(ObexOperation opcodeValue, bool isFinalBitSet)
    {
        Value = (byte)opcodeValue;
        switch (opcodeValue)
        {
            case OBEX.ObexOperation.Connect:
            case OBEX.ObexOperation.Disconnect:
            case OBEX.ObexOperation.SetPath:
            case OBEX.ObexOperation.Session:
            case OBEX.ObexOperation.Abort:
                if (!isFinalBitSet)
                    throw new ArgumentException(
                        nameof(IsFinalBitSet),
                        $"high bit of {opcodeValue} must be set"
                    );
                break;
            default:
                if (isFinalBitSet)
                    Value |= 0x80;
                break;
        }
    }

    /// <summary>
    ///     Construct a new ObexOpcode instance from byte
    /// </summary>
    /// <param name="rawOpcode">byte representation of opcode</param>
    /// <exception cref="InvalidObexOpcodeException">Throws if the provided byte is not a valid opcode</exception>
    public ObexOpcode(byte rawOpcode)
    {
        Value = rawOpcode;
        if (IsInUserDefinedRange)
        {
            if (!IsUserDefinedOperation(rawOpcode))
                throw new InvalidObexOpcodeException(rawOpcode);
        }
        else
        {
            if (ObexOperation == null)
                throw new InvalidObexOpcodeException(rawOpcode);
        }
    }

    public byte Value { get; }
    public bool IsFinalBitSet => Value >> 7 == 1;

    public bool IsInUserDefinedRange => Value is > 0x10 and <= 0x1F;

    /// <summary>
    ///     Return a <see cref="OBEX.ObexOperation" /> if this opcode represents a OBEX operation. Otherwise, return null.
    /// </summary>
    public ObexOperation? ObexOperation
    {
        get
        {
            var opcode = Value;
            if (!Enum.IsDefined(typeof(ObexOperation), opcode))
            {
                opcode = (byte)(opcode & 0x7F); // 0111 1111
                if (!Enum.IsDefined(typeof(ObexOperation), opcode))
                    return null;
            }

            return (ObexOperation?)opcode;
        }
    }

    /// <summary>
    ///     Whether the given opcode is a legal user-defined operation
    /// </summary>
    /// <param name="opcode"></param>
    /// <returns></returns>
    public bool IsUserDefinedOperation(byte opcode)
    {
        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is ObexOpcode opcode && Value == opcode.Value;
    }

    public override int GetHashCode()
    {
        return -1937169414 + Value.GetHashCode();
    }

    public override string ToString()
    {
        return $"Value: 0x{Value.ToString("X")}  Operation: {ObexOperation}";
    }
}
