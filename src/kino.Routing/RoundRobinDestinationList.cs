﻿using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core;
using kino.Core.Diagnostics;

namespace kino.Routing
{
    public class RoundRobinDestinationList : IRoundRobinDestinationList
    {
        private readonly ILogger logger;
        private readonly HashedLinkedList<IDestination> destinations;

        public RoundRobinDestinationList(ILogger logger)
        {
            this.logger = logger;
            destinations = new HashedLinkedList<IDestination>();
        }

        public IDestination SelectNextDestination(params IDestination[] receivers)
        {
            receivers = receivers.Where(d => d != null)
                                 .ToArray();
            var indexes = new List<int>();
            foreach (var receiver in receivers)
            {
                var index = destinations.IndexOf(receiver);
                if (index < 0)
                {
                    logger.Warn($"Destination [{receiver}] is not found in {GetType().Name}");
                    return receiver;
                }
                indexes.Add(index);
            }

            var nextDestinationIndex = indexes.Min();
            var destination = destinations.RemoveAt(nextDestinationIndex);
            destinations.InsertLast(destination);

            return destination;
        }

        public void Add(IDestination destination)
        {
            if (!destinations.Add(destination))
            {
                throw new Exception($"Destination [{destination}] already exists!");
            }
        }

        public void Remove(IDestination destination)
            => destinations.Remove(destination);
    }
}