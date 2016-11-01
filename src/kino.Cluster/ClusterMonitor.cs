﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Cluster
{
    public class ClusterMonitor : IClusterMonitor
    {
        private CancellationTokenSource messageProcessingToken;
        private readonly IClusterMembership clusterMembership;
        private Task sendingMessages;
        private Task listenningMessages;
        private readonly IRouterConfigurationProvider routerConfigurationProvider;
        private readonly IClusterMessageSender clusterMessageSender;
        private readonly IClusterMessageListener clusterMessageListener;
        private readonly IRouteDiscovery routeDiscovery;
        private readonly ISecurityProvider securityProvider;

        public ClusterMonitor(IRouterConfigurationProvider routerConfigurationProvider,
                              IClusterMembership clusterMembership,
                              IClusterMessageSender clusterMessageSender,
                              IClusterMessageListener clusterMessageListener,
                              IRouteDiscovery routeDiscovery,
                              ISecurityProvider securityProvider)
        {
            this.routerConfigurationProvider = routerConfigurationProvider;
            this.clusterMessageSender = clusterMessageSender;
            this.clusterMessageListener = clusterMessageListener;
            this.routeDiscovery = routeDiscovery;
            this.securityProvider = securityProvider;
            this.clusterMembership = clusterMembership;
        }

        public bool Start(TimeSpan startTimeout)
            => StartProcessingClusterMessages(startTimeout);

        public void Stop()
            => StopProcessingClusterMessages();

        private bool StartProcessingClusterMessages(TimeSpan startTimeout)
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => clusterMessageSender.StartBlockingSendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages =
                    Task.Factory.StartNew(_ => clusterMessageListener.StartBlockingListenMessages(RestartProcessingClusterMessages, messageProcessingToken.Token, gateway),
                                          TaskCreationOptions.LongRunning);
                var started = gateway.SignalAndWait(startTimeout, messageProcessingToken.Token);
                if (started)
                {
                    routeDiscovery.Start();
                }

                return started;
            }
        }

        private void StopProcessingClusterMessages()
        {
            routeDiscovery.Stop();
            messageProcessingToken?.Cancel();
            sendingMessages?.Wait();
            listenningMessages?.Wait();
            messageProcessingToken?.Dispose();
        }

        private void RestartProcessingClusterMessages()
        {
            StopProcessingClusterMessages();
            StartProcessingClusterMessages(TimeSpan.FromMilliseconds(-1));
        }

        public void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain)
        {
            var scaleOutAddress = routerConfigurationProvider.GetScaleOutAddress();

            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = scaleOutAddress.Identity,
                                             MessageContracts = messageHandlers.Select(mi => new Messaging.Messages.MessageContract
                                                                                             {
                                                                                                 Version = mi.Version,
                                                                                                 Identity = mi.Identity,
                                                                                                 Partition = mi.Partition,
                                                                                                 IsAnyIdentifier = mi is AnyIdentifier
                                                                                             }).ToArray()
                                         },
                                         domain);
            message.As<Message>().SignMessage(securityProvider);
            clusterMessageSender.EnqueueMessage(message);
        }

        public void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers)
        {
            var scaleOutAddress = routerConfigurationProvider.GetScaleOutAddress();
            var messageGroups = GetMessageHubs(messageIdentifiers).Concat(GetMessageHandlers(messageIdentifiers))
                                                                  .GroupBy(mh => mh.Domain);

            foreach (var group in messageGroups)
            {
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                                 SocketIdentity = scaleOutAddress.Identity,
                                                 MessageContracts = group.Select(g => g.Message).ToArray()
                                             },
                                             group.Key);
                message.As<Message>().SignMessage(securityProvider);

                clusterMessageSender.EnqueueMessage(message);
            }
        }

        private IEnumerable<MessageDomainMap> GetMessageHandlers(IEnumerable<Identifier> messageIdentifiers)
            => messageIdentifiers.Where(mi => !mi.IsMessageHub())
                                 .Select(mi => new MessageDomainMap
                                               {
                                                   Message = new Messaging.Messages.MessageContract
                                                             {
                                                                 Identity = mi.Identity,
                                                                 Version = mi.Version,
                                                                 Partition = mi.Partition,
                                                                 IsAnyIdentifier = false
                                                             },
                                                   Domain = securityProvider.GetDomain(mi.Identity)
                                               });

        private IEnumerable<MessageDomainMap> GetMessageHubs(IEnumerable<Identifier> messageIdentifiers)
            => messageIdentifiers.Where(mi => mi.IsMessageHub())
                                 .SelectMany(mi => securityProvider.GetAllowedDomains().Select(dom =>
                                                                                                   new MessageDomainMap
                                                                                                   {
                                                                                                       Message = new Messaging.Messages.MessageContract
                                                                                                                 {
                                                                                                                     Identity = mi.Identity,
                                                                                                                     Version = mi.Version,
                                                                                                                     Partition = mi.Partition,
                                                                                                                     IsAnyIdentifier = true
                                                                                                                 },
                                                                                                       Domain = dom
                                                                                                   }));

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembership.GetClusterMembers();

        public void DiscoverMessageRoute(Identifier messageIdentifier)
            => routeDiscovery.RequestRouteDiscovery(messageIdentifier);
    }

    internal class MessageDomainMap
    {
        internal Messaging.Messages.MessageContract Message { get; set; }

        internal string Domain { get; set; }
    }
}