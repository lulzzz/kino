﻿using kino.Routing.ServiceMessageHandlers;
using NUnit.Framework;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    [TestFixture]
    public class PingHandlerTests
    {
        [Test]
        public void PingHandler_DoesNothing()
        {
            new PingHandler().Handle(null, null);
        }
    }
}