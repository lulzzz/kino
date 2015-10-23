﻿using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestClusterMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "REQCLUSTROUTES".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string RequestorUri { get; set; }

        [ProtoMember(2)]
        public byte[] RequestorSocketIdentity { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}