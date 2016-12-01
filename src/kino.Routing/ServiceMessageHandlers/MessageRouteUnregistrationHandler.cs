﻿using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class MessageRouteUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterServices clusterServices;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public MessageRouteUnregistrationHandler(IClusterServices clusterServices,
                                                 IExternalRoutingTable externalRoutingTable,
                                                 ISecurityProvider securityProvider,
                                                 ILogger logger)
        {
            this.clusterServices = clusterServices;
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterMessageRouting(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                    var nodeIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
                    foreach (var route in GetUnregistrationRoutes(payload, message.Domain))
                    {
                        var peerRemoveResult = externalRoutingTable.RemoveMessageRoute(route);
                        if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
                        {
                            forwardingSocket.SafeDisconnect(peerRemoveResult.Uri);
                        }
                        if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
                        {
                            clusterServices.DeletePeer(nodeIdentifier);
                        }
                    }
                }
            }

            return shouldHandle;
        }

        private IEnumerable<ExternalRouteRemoval> GetUnregistrationRoutes(UnregisterMessageRouteMessage payload, string domain)
        {
            var peer = new Node(payload.Uri, payload.ReceiverNodeIdentity);
            foreach (var route in payload.Routes.SelectMany(r => r.MessageContracts.Select(mc => new MessageRoute
                                                                                                 {
                                                                                                     Receiver = new ReceiverIdentifier(r.ReceiverIdentity),
                                                                                                     Message = new MessageIdentifier(mc.Identity, mc.Version, mc.Partition)
                                                                                                 })))
            {
                if (route.Receiver.IsMessageHub() || securityProvider.GetDomain(route.Message.Identity) == domain)
                {
                    yield return new ExternalRouteRemoval
                                 {
                                     Route = route,
                                     Peer = peer
                                 };
                }
                else
                {
                    logger.Warn($"MessageIdentity {route.Message} doesn't belong to requested Domain {domain}!");
                }
            }
        }

        private bool IsUnregisterMessageRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterMessageRoute);
    }
}