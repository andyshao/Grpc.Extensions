﻿using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Grpc.Extension.LoadBalancer;
using Grpc.Extension.Model;
using Grpc.Extension.Discovery;

namespace Grpc.Extension.Internal
{
    /// <summary>
    /// Channel统一管理
    /// </summary>
    internal class ChannelManager
    {
        private ConcurrentDictionary<string, ChannelInfo> _channels = new ConcurrentDictionary<string, ChannelInfo>();
        private IServiceDiscovery _serviceDiscovery;
        private ILoadBalancer _loadBalancer;

        /// <summary>
        /// Channel统一管理
        /// </summary>
        /// <param name="serviceDiscovery"></param>
        /// <param name="loadBalancer"></param>
        public ChannelManager(IServiceDiscovery serviceDiscovery, ILoadBalancer loadBalancer)
        {
            this._serviceDiscovery = serviceDiscovery;
            this._loadBalancer = loadBalancer;
        }

        internal List<ChannelConfig> Configs { get; set; } = new List<ChannelConfig>();

        /// <summary>
        /// 根据客户端代理类型获取channel
        /// </summary>
        public Channel GetChannel(string grpcServiceName)
        {
            var config = Configs?.FirstOrDefault(q => q.GrpcServiceName == grpcServiceName?.Trim());
            if (config == null)
            {
                LoggerAccessor.Instance.LoggerError?.Invoke(new Exception($"GetChannel({grpcServiceName ?? ""}) has not exists"));
                return null;
            }
            if (config.UseDirect)
            {
                return GetChannelCore(config.DirectEndpoint,config.DiscoveryServiceName);
            }
            else//from discovery
            {
                var endPoint = GetEndpoint(config.DiscoveryServiceName, config.DiscoveryUrl);
                return GetChannelCore(endPoint,config.DiscoveryServiceName);
            }
        }

        /// <summary>
        /// 根据服务名称返回服务地址
        /// </summary>
        private string GetEndpoint(string serviceName, string consulUrl)
        {
            //获取健康的endpoints
            var healthEndpoints = _serviceDiscovery.GetEndpoints(serviceName, consulUrl);
            if (healthEndpoints == null || healthEndpoints.Count == 0)
            {
                throw new Exception($"get endpoints from consul of {serviceName} is null");
            }
            //获取错误的channel
            var errorChannel = _channels.Where(p => p.Value.DiscoveryServiceName == serviceName &&
                                                !healthEndpoints.Contains(p.Key)).ToList();
            //关闭并删除错误的channel
            foreach (var channel in errorChannel)
            {
                channel.Value.Channel.ShutdownAsync();
                _channels.TryRemove(channel.Key,out var tmp);
            }
            
            return _loadBalancer.SelectEndpoint(serviceName, healthEndpoints);
        }

        private Channel GetChannelCore(string endpoint,string serviceName)
        {
            Func<string, ChannelInfo> addFunc = key =>
                new ChannelInfo()
                {
                    DiscoveryServiceName = serviceName,
                    Channel = new Channel(key, ChannelCredentials.Insecure)
                };
            //获取channel，不存在就添加
            var channel = _channels.GetOrAdd(endpoint, addFunc).Channel;
            //检查channel状态
            if (channel.State == ChannelState.Shutdown || channel.State == ChannelState.TransientFailure)
            {
                //状态异常就关闭后重建
                channel.ShutdownAsync();
                //新增或者修改channel
                return _channels.AddOrUpdate(endpoint, addFunc, (key, value) => new ChannelInfo()
                    {
                        DiscoveryServiceName = serviceName,
                        Channel = new Channel(key, ChannelCredentials.Insecure)
                    }).Channel;
            }
            else
            {
                return channel;
            }
        }

        /// <summary>
        /// 关闭Channel
        /// </summary>
        public void Shutdown()
        {
            _channels.Select(q => q.Value).ToList().ForEach(q => q.Channel.ShutdownAsync().Wait());
        }
    }
}
