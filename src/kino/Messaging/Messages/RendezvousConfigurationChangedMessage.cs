﻿using System.Collections.Generic;
using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousConfigurationChangedMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "RNDZVRECONFIG".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public IEnumerable<RendezvousNode> RendezvousNodes { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}