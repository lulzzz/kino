﻿using rawf.Connectivity;

namespace Server
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetConfiguration();
    }
}