﻿using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace Console.Messages
{
    [ProtoContract]
    public class EhlloMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "EHHLO".GetBytes();

        [ProtoMember(1)]
        public string Ehllo { get; set; }
    }
}