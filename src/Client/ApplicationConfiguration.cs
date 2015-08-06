﻿using System.Collections.Generic;

namespace Client
{
    public class ApplicationConfiguration
    {
        public string RouterUri { get; set; }
        public string ScaleOutAddressUri { get; set; }
        public IEnumerable<RendezvousEndpoint> RendezvousServers { get; set; }
    }
}