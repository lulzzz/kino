﻿using System;
using System.Linq;
using System.Threading;
using kino.Actors;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using Xunit;

namespace kino.Tests.Actors
{
    public class ActorHostTests
    {
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(500);
        private readonly Mock<ILogger> logger;
        private readonly ActorHandlerMap actorHandlersMap;
        private readonly Mock<ISecurityProvider> securityProvider;
        private ActorHost actorHost;
        private readonly Mock<ILocalSocket<IMessage>> localRouterSocket;
        private readonly Mock<ILocalSendingSocket<InternalRouteRegistration>> internalRegistrationSender;
        private readonly Mock<ILocalSocketFactory> localSocketFactory;
        private readonly Mock<ILocalSocket<IMessage>> receivingSocket;
        private readonly Mock<IAsyncQueue<AsyncMessageContext>> asyncQueue;

        public ActorHostTests()
        {
            logger = new Mock<ILogger>();
            actorHandlersMap = new ActorHandlerMap();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            localRouterSocket = new Mock<ILocalSocket<IMessage>>();
            internalRegistrationSender = new Mock<ILocalSendingSocket<InternalRouteRegistration>>();
            localSocketFactory = new Mock<ILocalSocketFactory>();
            receivingSocket = new Mock<ILocalSocket<IMessage>>();
            localSocketFactory.Setup(m => m.Create<IMessage>()).Returns(receivingSocket.Object);
            asyncQueue = new Mock<IAsyncQueue<AsyncMessageContext>>();
            actorHost = new ActorHost(actorHandlersMap,
                                      asyncQueue.Object,
                                      new AsyncQueue<ActorRegistration>(),
                                      securityProvider.Object,
                                      localRouterSocket.Object,
                                      internalRegistrationSender.Object,
                                      localSocketFactory.Object,
                                      logger.Object);
        }

        [Fact]
        public void AssignActor_SendsRegisterationMessage()
        {
            var partition = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
            try
            {
                StartActorHost(actorHost);
                actorHost.AssignActor(new ConfigurableActor(new[]
                                                            {
                                                                new MessageHandlerDefinition
                                                                {
                                                                    Handler = _ => null,
                                                                    Message = new MessageDefinition(messageIdentifier.Identity,
                                                                                                    messageIdentifier.Version,
                                                                                                    partition)
                                                                }
                                                            }));
                AsyncOp.Sleep();
                Func<InternalRouteRegistration, bool> registrationRequest = (reg) => reg.MessageContracts.Any(id => Unsafe.ArraysEqual(id.Message.Identity, messageIdentifier.Identity)
                                                                                                                    && Unsafe.ArraysEqual(id.Message.Partition, messageIdentifier.Partition)
                                                                                                                    && id.Message.Version == messageIdentifier.Version);
                internalRegistrationSender.Verify(m => m.Send(It.Is<InternalRouteRegistration>(reg => registrationRequest(reg))), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void SyncActorResponse_SendImmediately()
        {
            try
            {
                actorHost.AssignActor(new EchoActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage());
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);

                localRouterSocket.WaitUntilMessageSent();
                //
                asyncQueue.Verify(m => m.Enqueue(It.IsAny<AsyncMessageContext>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void ExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var errorMessage = Guid.NewGuid().ToString();

            try
            {
                actorHost.AssignActor(new ExceptionActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage {Content = errorMessage});
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                localRouterSocket.WaitUntilMessageSent(IsExceptionMessage);

                bool IsExceptionMessage(IMessage message)
                    => message.GetPayload<ExceptionMessage>().Exception.Message == errorMessage;
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void AsyncActorResult_IsEnqueuedForCompletion()
        {
            try
            {
                actorHost.AssignActor(new EchoActor());

                var asyncMessage = new AsyncMessage {Delay = AsyncOp};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                (AsyncOpCompletionDelay + AsyncOp).Sleep();
                //
                asyncQueue.Verify(m => m.Enqueue(It.IsAny<AsyncMessageContext>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void AsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        {
            try
            {
                actorHost.AssignActor(new ExceptionActor());
                var error = Guid.NewGuid().ToString();
                var asyncMessage = new AsyncExceptionMessage
                                   {
                                       Delay = AsyncOp,
                                       ErrorMessage = error
                                   };
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                (AsyncOpCompletionDelay + AsyncOp).Sleep();
                //
                asyncQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(c => c.OutMessages.First().Equals(KinoMessages.Exception)), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void AsyncActorResult_IsAddedToMessageCompletionQueue()
        {
            try
            {
                actorHost.AssignActor(new EchoActor());
                var delay = AsyncOp;
                var asyncMessage = new AsyncMessage {Delay = delay};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                (AsyncOpCompletionDelay + AsyncOp).Sleep();

                asyncQueue.Verify(m => m.Enqueue(It.IsAny<AsyncMessageContext>(), It.IsAny<CancellationToken>()),
                                  Times.Once);
                asyncQueue.Verify(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void CallbackReceiverIdentities_AreCopiedFromIncomingMessageProcessedSync()
        {
            try
            {
                actorHost.AssignActor(new EchoActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                var callbackReceiverNode = Guid.NewGuid().ToByteArray();
                messageIn.RegisterCallbackPoint(callbackReceiverNode,
                                                callbackReceiver,
                                                MessageIdentifier.Create<SimpleMessage>(),
                                                Randomizer.Int32());
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                localRouterSocket.WaitUntilMessageSent(AssertCallbackPropertiesCopied);

                bool AssertCallbackPropertiesCopied(Message messageOut)
                    => messageIn.CallbackPoint.SequenceEqual(messageOut.CallbackPoint)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverNodeIdentity, messageOut.CallbackReceiverNodeIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void CallbackReceiverIdentities_AreCopiedFromIncomingMessageProcessedAsync()
        {
            try
            {
                actorHost = new ActorHost(actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<ActorRegistration>(),
                                          securityProvider.Object,
                                          localRouterSocket.Object,
                                          internalRegistrationSender.Object,
                                          localSocketFactory.Object,
                                          logger.Object);
                actorHost.AssignActor(new EchoActor());
                var asyncMessage = new AsyncMessage {Delay = AsyncOp};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage).As<Message>();
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                var callbackReceiverNode = Guid.NewGuid().ToByteArray();
                messageIn.RegisterCallbackPoint(callbackReceiverNode,
                                                callbackReceiver,
                                                MessageIdentifier.Create<SimpleMessage>(),
                                                Randomizer.Int32());
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                localRouterSocket.WaitUntilMessageSent(AssertCallbackPropertiesCopied);

                bool AssertCallbackPropertiesCopied(Message messageOut)
                    => messageIn.CallbackPoint.SequenceEqual(messageOut.CallbackPoint)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverNodeIdentity, messageOut.CallbackReceiverNodeIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void IfCallbackIsRegistered_SyncExceptionMessageIsDeliveredToCallbackReceiver()
        {
            try
            {
                actorHost.AssignActor(new ExceptionActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                var callbackReceiverNode = Guid.NewGuid().ToByteArray();
                var callbackPoints = new[] {MessageIdentifier.Create<SimpleMessage>(), KinoMessages.Exception};
                messageIn.RegisterCallbackPoint(callbackReceiverNode,
                                                callbackReceiver,
                                                callbackPoints,
                                                Randomizer.Int32());
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                localRouterSocket.WaitUntilMessageSent(AssertCallbackPropertiesCopied);

                bool AssertCallbackPropertiesCopied(Message messageOut)
                    => messageOut.Equals(KinoMessages.Exception)
                       && messageIn.CallbackPoint.SequenceEqual(messageOut.CallbackPoint)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverNodeIdentity, messageOut.CallbackReceiverNodeIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void IfCallbackIsRegistered_AsyncExceptionMessageIsDeliveredToCallbackReceiver()
        {
            try
            {
                actorHost = new ActorHost(actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<ActorRegistration>(),
                                          securityProvider.Object,
                                          localRouterSocket.Object,
                                          internalRegistrationSender.Object,
                                          localSocketFactory.Object,
                                          logger.Object);
                actorHost.AssignActor(new ExceptionActor());
                var messageIn = Message.CreateFlowStartMessage(new AsyncExceptionMessage {Delay = AsyncOp}).As<Message>();
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                var callbackReceiverNode = Guid.NewGuid().ToByteArray();
                var callbackPoints = new[] {MessageIdentifier.Create<SimpleMessage>(), KinoMessages.Exception};
                messageIn.RegisterCallbackPoint(callbackReceiverNode,
                                                callbackReceiver,
                                                callbackPoints,
                                                Randomizer.Int32());
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                localRouterSocket.WaitUntilMessageSent(AssertCallbackPropertiesCopied);

                bool AssertCallbackPropertiesCopied(Message messageOut)
                    => messageOut.Equals(KinoMessages.Exception)
                       && messageIn.CallbackPoint.SequenceEqual(messageOut.CallbackPoint)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity)
                       && Unsafe.ArraysEqual(messageIn.CallbackReceiverNodeIdentity, messageOut.CallbackReceiverNodeIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Fact]
        public void ExceptionMessage_HasDomainSet()
        {
            var kinoDomain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(KinoMessages.Exception.Identity)).Returns(kinoDomain);
            try
            {
                actorHost = new ActorHost(actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<ActorRegistration>(),
                                          securityProvider.Object,
                                          localRouterSocket.Object,
                                          internalRegistrationSender.Object,
                                          localSocketFactory.Object,
                                          logger.Object);
                actorHost.AssignActor(new ExceptionActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage());
                receivingSocket.SetupMessageReceived(messageIn);
                //
                StartActorHost(actorHost);
                //
                localRouterSocket.WaitUntilMessageSent(AssertCallbackPropertiesCopied);

                bool AssertCallbackPropertiesCopied(Message messageOut)
                    => messageOut.Equals(KinoMessages.Exception)
                       && messageOut.Domain == kinoDomain;
            }
            finally
            {
                actorHost.Stop();
            }
        }

        private static void StartActorHost(IActorHost actorHost)
        {
            actorHost.Start();
            AsyncOpCompletionDelay.Sleep();
        }
    }
}