﻿using kino.Routing.ServiceMessageHandlers;
using Xunit;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class PingHandlerTests
    {
        [Fact]
        public void PingHandler_DoesNothing()
            => new PingHandler().Handle(null, null);
    }
}