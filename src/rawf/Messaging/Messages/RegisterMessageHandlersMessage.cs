﻿using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RegisterMessageHandlersMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "MSGHREG".GetBytes();

        [ProtoMember(1)]
        public MessageHandlerRegistration[] Registrations { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }
    }
}