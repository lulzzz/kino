﻿using System;
using kino.Actors;
using kino.Client;

namespace kino
{
    public partial class kino : IDisposable
    {
        private IDependencyResolver resolver;
        private bool isStarted;

        public void SetResolver(IDependencyResolver resolver)
            => this.resolver = resolver;

        public IMessageHub GetMessageHub()
        {
            AssertKinoStarted();
            return getMessageHub();
        }

        public IMessageHub CreateMessageHub(bool keepRegistrationLocal)
        {
            AssertKinoStarted();
            return createMessageHub(keepRegistrationLocal);
        }

        public void AssignActor(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy = ActorHostInstancePolicy.TryReuseExisting)
        {
            AssertKinoStarted();
            actorHostManager.AssignActor(actor, actorHostInstancePolicy);
        }

        public void Start()
        {
            AssertDependencyResolverSet();

            Build();

            messageRouter.Start();
            isStarted = true;
        }

        private void AssertDependencyResolverSet()
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver), "Dependency resolver is not assigned!");
            }
        }

        public void Stop()
        {
            messageRouter.Stop();
            isStarted = false;
        }

        public void Dispose()
        {
            actorHostManager.Dispose();
        }

        private void AssertKinoStarted()
        {
            if (!isStarted)
            {
                throw new InvalidOperationException("Kino is not started yet! Call kino.Start() must happen first.");
            }
        }
    }
}