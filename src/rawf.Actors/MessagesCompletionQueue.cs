﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using rawf.Connectivity;

namespace rawf.Actors
{
    public class MessagesCompletionQueue : IMessagesCompletionQueue
    {
        private readonly BlockingCollection<AsyncMessageContext> asyncResponses;

        public MessagesCompletionQueue()
        {
            asyncResponses = new BlockingCollection<AsyncMessageContext>(new ConcurrentQueue<AsyncMessageContext>());
        }

        public IEnumerable<AsyncMessageContext> GetMessages(CancellationToken cancellationToken)
            => asyncResponses.GetConsumingEnumerable(cancellationToken);

        public void Enqueue(AsyncMessageContext messageCompletion, CancellationToken cancellationToken)
            => asyncResponses.Add(messageCompletion, cancellationToken);

        public void Dispose()
            => asyncResponses.Dispose();
    }
}