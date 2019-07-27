﻿using System.Collections.Generic;

namespace Grpc.Extension.Discovery
{
    /// <summary>
    /// 服务发现
    /// </summary>
    public interface IServiceDiscovery
    {
        /// <summary>
        /// 获取服务地址列表
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="discoveryUrl"></param>
        /// <returns></returns>
        List<string> GetEndpoints(string serviceName, string discoveryUrl);
    }
}