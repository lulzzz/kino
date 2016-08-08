﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Autofac;
using Client;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Security;
using Server.Actors;
using TypedConfigProvider;

namespace Server
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new Logger("default"))
                   .As<ILogger>()
                   .SingleInstance();

            builder.RegisterType<RevertStringActor>()
                   .As<IActor>()
                   .SingleInstance();

            builder.RegisterType<GroupCharsActor>()
                   .As<IActor>()
                   .SingleInstance();

            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<ApplicationConfiguration>())
                   .As<ApplicationConfiguration>()
                   .SingleInstance();

            builder.RegisterType<ConfigurationProvider>()
                   .As<IConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRouterConfiguration())
                   .As<RouterConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRendezvousEndpointsConfiguration())
                   .As<IEnumerable<RendezvousEndpoint>>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterTimingConfiguration())
                   .As<ClusterMembershipConfiguration>()
                   .SingleInstance();

            builder.RegisterType<SecurityProvider>()
                   .As<ISecurityProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainPrivateKeyProvider>()
                   .As<IDomainPrivateKeyProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainScopeResolver>()
                   .As<IDomainScopeResolver>()
                   .SingleInstance();

            builder.Register(m => HMACMD5.Create())
                   .As<Func<HMAC>>()
                   .SingleInstance();
        }
    }
}