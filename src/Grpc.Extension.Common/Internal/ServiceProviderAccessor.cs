﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Grpc.Extension.Common.Internal
{
    /// <summary>
    /// ServiceProviderAccessor
    /// </summary>
    public class ServiceProviderAccessor
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static void SetServiceProvider(IServiceProvider sp)
        {
            ServiceProvider = sp;
        }

        public static T GetService<T>()
        {
            return ServiceProvider.GetService<T>();
        }

        public static List<T> GetServices<T>()
        {
            return ServiceProvider.GetServices<T>()?.ToList();
        }
    }
}
