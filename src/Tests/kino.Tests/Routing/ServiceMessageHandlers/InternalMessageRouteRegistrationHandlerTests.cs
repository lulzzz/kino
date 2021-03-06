﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Helpers;
using Moq;
using Xunit;
using MessageRoute = kino.Cluster.MessageRoute;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class InternalMessageRouteRegistrationHandlerTests
    {
        private readonly InternalMessageRouteRegistrationHandler handler;
        private readonly Mock<IClusterMonitor> clusterMonitor;
        private readonly Mock<IInternalRoutingTable> internalRoutingTable;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly Mock<ILocalSendingSocket<IMessage>> destinationSocket;
        private readonly string domain;

        public InternalMessageRouteRegistrationHandlerTests()
        {
            clusterMonitor = new Mock<IClusterMonitor>();
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            destinationSocket = new Mock<ILocalSendingSocket<IMessage>>();
            handler = new InternalMessageRouteRegistrationHandler(clusterMonitor.Object,
                                                                  internalRoutingTable.Object,
                                                                  securityProvider.Object);
        }

        [Fact]
        public void IfReceiverIdentifierIsNeitherActorNorMessageHub_MessageRouteIsNotAdded()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray())
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<InternalRouteRegistration>()), Times.Never);
        }

        [Fact]
        public void LocalyRegisteredMessageHub_IsRegisteredInLocalRoutingTableButNotInCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                        KeepRegistrationLocal = true,
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()),
                                  Times.Never);
        }

        [Fact]
        public void GlobalyRegisteredMessageHub_IsRegisteredInLocalRoutingTableAndCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                        KeepRegistrationLocal = false,
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(routes => routes.Any(r => r.Receiver == routeRegistration.ReceiverIdentifier)),
                                                      domain),
                                  Times.Once);
        }

        [Fact]
        public void GlobalyRegisteredMessageHub_IsRegisteredInClusterOncePerEachDomain()
        {
            var allowedDomains = Randomizer.Int32(2, 5)
                                           .Produce(() => Guid.NewGuid().ToString());
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                        KeepRegistrationLocal = false,
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(routes => routes.Any(r => r.Receiver == routeRegistration.ReceiverIdentifier)),
                                                      It.Is<string>(d => allowedDomains.Contains(d))),
                                  Times.Exactly(allowedDomains.Count()));
        }

        [Fact]
        public void LocalyRegisteredMessageRoutes_AreRegisteredInLocalRoutingTableButNotInCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                        MessageContracts = Randomizer.Int32(2, 5)
                                                                     .Produce(() => new MessageContract
                                                                                    {
                                                                                        Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                        Randomizer.UInt16(),
                                                                                                                        Guid.NewGuid().ToByteArray()),
                                                                                        KeepRegistrationLocal = true
                                                                                    }),
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void OnlyGlobalyRegisteredMessageRoutes_AreRegisteredInLocalRoutingTableButAndCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                        MessageContracts = Randomizer.Int32(5, 15)
                                                                     .Produce(i => new MessageContract
                                                                                   {
                                                                                       Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                       Randomizer.UInt16(),
                                                                                                                       Guid.NewGuid().ToByteArray()),
                                                                                       KeepRegistrationLocal = i % 2 == 0
                                                                                   }),
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            Func<IEnumerable<MessageRoute>, bool> areGlobalMessageRoutes = mrs =>
                                                                           {
                                                                               routeRegistration.MessageContracts
                                                                                                .Where(mc => !mc.KeepRegistrationLocal)
                                                                                                .Select(mc => mc.Message)
                                                                                                .Should()
                                                                                                .BeEquivalentTo(mrs.Select(mr => mr.Message));
                                                                               return true;
                                                                           };
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => areGlobalMessageRoutes(mrs)), domain), Times.Once);
        }

        [Fact]
        public void MessageRouteRegistrations_AreGroupedByDomainWhenRegisteredAtCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                        MessageContracts = Randomizer.Int32(5, 15)
                                                                     .Produce(i => new MessageContract
                                                                                   {
                                                                                       Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                       Randomizer.UInt16(),
                                                                                                                       Guid.NewGuid().ToByteArray())
                                                                                   }),
                                        DestinationSocket = destinationSocket.Object
                                    };
            var secondDomain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(routeRegistration.MessageContracts.First().Message.Identity)).Returns(secondDomain);
            var allowedDomains = new[] {domain, secondDomain};
            //
            handler.Handle(routeRegistration);
            //
            Func<IEnumerable<MessageRoute>, bool> areGlobalMessageRoutes = mrs =>
                                                                           {
                                                                               mrs.Select(mr => mr.Message)
                                                                                  .Should()
                                                                                  .BeSubsetOf(routeRegistration.MessageContracts
                                                                                                               .Select(mc => mc.Message));
                                                                               return true;
                                                                           };
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => areGlobalMessageRoutes(mrs)),
                                                      It.Is<string>(d => allowedDomains.Contains(d))),
                                  Times.Exactly(allowedDomains.Length));
        }
    }
}