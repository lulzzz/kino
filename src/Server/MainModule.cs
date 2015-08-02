﻿using System.Collections.Generic;
using Autofac;
using rawf.Connectivity;
using TypedConfigProvider;

namespace Server
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new rawf.Actors.MainModule());

            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.RegisterType<RouterConfigurationProvider>()
                   .As<IRouterConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IInitialRendezvousServerConfiguration>().GetConfiguration())
                   .As<IEnumerable<RendezvousServerConfiguration>>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IRouterConfigurationProvider>().GetConfiguration())
                   .As<IRouterConfiguration>()
                   .SingleInstance();

            builder.RegisterType<InitialRendezvousServerConfiguration>()
                   .As<IInitialRendezvousServerConfiguration>()
                   .SingleInstance();
        }
    }
}