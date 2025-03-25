﻿using System;
using System.Buffers.Binary;
using GoodTimeStudio.MyPhone.OBEX.Streams;

namespace GoodTimeStudio.MyPhone.OBEX.Headers
{
    public class AppParameterHeaderInterpreter : IBufferContentInterpreter<AppParameterDictionary>
    {
        public AppParameterDictionary GetValue(ReadOnlySpan<byte> buffer)
        {
            AppParameterDictionary dict = new();
            for (int i = 0; i < buffer.Length; )
            {
                byte tagId = buffer[i++];
                byte len = BinaryPrimitives.ReverseEndianness(buffer[i++]);
                dict[tagId] = new AppParameter(tagId, buffer.Slice(i, len).ToArray());
                i += len;
            }
            return dict;
        }
    }
}
