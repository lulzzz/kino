﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Actors;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Tests.Actor.Setup;

namespace rawf.Tests.Actor
{
    [TestFixture]
    public class ActorHostTests
    {
        [Test]
        public void TestAssignActor_RegistersActorHandlers()
        {
            var actorHandlersMap = new ActorHandlersMap();

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          new ConnectivityProvider(),
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());

            var registration = actorHandlersMap.GetRegisteredIdentifiers().First();
            CollectionAssert.AreEqual(EmptyMessage.MessageIdentity, registration.Identity);
            CollectionAssert.AreEqual(Message.CurrentVersion, registration.Version);
        }

        [Test]
        public void TestStartingActorHost_SendsActorRegistrationMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var registration = socket.GetSentMessages().First();
            var payload = new RegisterMessageHandlers
                          {
                              SocketIdentity = socket.GetIdentity(),
                              Registrations = actorHandlersMap
                                  .GetRegisteredIdentifiers()
                                  .Select(mh => new MessageHandlerRegistration
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version,
                                                    IdentityType = IdentityType.Actor
                                                })
                                  .ToArray()
                          };
            var regMessage = Message.Create(payload, RegisterMessageHandlers.MessageIdentity);

            CollectionAssert.AreEqual(registration.Body, regMessage.Body);
        }

        [Test]
        [ExpectedException]
        public void TestStartingActorHostWithoutActorAssigned_ThrowsException()
        {
            var actorHandlersMap = new ActorHandlersMap();

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          new ConnectivityProvider(),
                                          new HostConfiguration(string.Empty));
            actorHost.Start();
        }

        [Test]
        public void TestSyncActorResponse_SendImmediately()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new EmptyMessage(), EmptyMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(TimeSpan.FromMilliseconds(100));

            var messageOut = socket.GetSentMessages().Last();

            CollectionAssert.AreEqual(messageOut.Body, messageIn.Body);
        }

        [Test]
        public void TestExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var errorMessage = "Bla";
            var actorHost = new ActorHost(actorHandlersMap, new MessagesCompletionQueue(), connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new ExceptionActor(errorMessage));
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new EmptyMessage(), EmptyMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(TimeSpan.FromMilliseconds(500));

            var messageOut = socket.GetSentMessages().Last();

            Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
        }

        [Test]
        public void TestAsyncActorResultIsSentAfterCompletion_DeliveredAsExceptionMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);
            var messageCompletionQueue = new Mock<IMessagesCompletionQueue>();
            messageCompletionQueue.Setup(m => m.GetMessages(It.IsAny<CancellationToken>()))
                                  .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

            var actorHost = new ActorHost(actorHandlersMap,
                                          messageCompletionQueue.Object,
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var delay = TimeSpan.FromMilliseconds(200);
            var asyncMessage = new AsyncMessage {Delay = delay};
            var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(delay);
            Thread.Sleep(TimeSpan.FromMilliseconds(500));

            messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncMessage(amc)),
                                                    It.IsAny<CancellationToken>()), Times.Once);
            messageCompletionQueue.Verify(m => m.GetMessages(It.IsAny<CancellationToken>()), Times.Once);
        }

        private static bool IsAsyncMessage(AsyncMessageContext amc)
        {
            return Unsafe.Equals(amc.OutMessage.Identity, AsyncMessage.MessageIdentity);
        }
    }
}