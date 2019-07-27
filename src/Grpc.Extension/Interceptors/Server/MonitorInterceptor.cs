﻿using Grpc.Core.Interceptors;
using System;
using System.Collections.Generic;
using System.Text;
using Grpc.Core;
using Grpc.Extension.Model;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Extension.Common;
using Grpc.Extension.Internal;
using Grpc.Core.Utils;

namespace Grpc.Extension.Interceptors
{
    /// <summary>
    /// 性能监控,记录日志
    /// </summary>
    internal class MonitorInterceptor : ServerInterceptor
    {
        private async Task<TResponse> Monitor<TRequest, TResponse>(object request, 
            ServerCallContext context, Delegate continuation, object response = null)
        {
            var trace = context.RequestHeaders.FirstOrDefault(q => q.Key == Consts.TraceId);
            if (trace == null)
            {
                trace = new Metadata.Entry(Consts.TraceId, Guid.NewGuid().ToString());
                context.RequestHeaders.Add(trace);
            }
            var model = new MonitorModel
            {
                ClientIp = context.Peer,
                RequestUrl = context.Method,
                //RequestData = request?.ToJson(),
                TraceId = trace.Value
            };
            if (request is TRequest)
            {
                model.RequestData = request?.ToJson();
            }
            else if(request is IAsyncStreamReader<TRequest>)
            {
                var requests = new List<TRequest>();
                //await requestStream.ForEachAsync(req=> {
                //    requests.Add(req);
                //    return Task.CompletedTask;
                //});
                model.RequestData = requests?.ToJson();
            }
            try
            {
                if (response == null)
                {
                    var result = await (continuation.DynamicInvoke(request, context) as Task<TResponse>);
                    model.Status = "ok";

                    model.ResponseData = MonitorManager.Instance.SaveResponseMethodEnable(context.Method) ? result?.ToJson() : Consts.NotResponseMsg;

                    return result;
                }
                else
                {
                    await (continuation.DynamicInvoke(request, context) as Task);
                    return default(TResponse);
                }
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aex)
                {
                    foreach (var e in aex.Flatten().InnerExceptions)
                    {
                        model.Exception += e?.ToString() + Environment.NewLine;
                    }
                }
                else
                {
                    model.Exception = ex?.ToString();
                }

                model.Status = "error";
                LoggerAccessor.Instance.LoggerError?.Invoke(new Exception(model.Exception));
                throw CommonError.BuildRpcException(ex);
            }
            finally
            {
                model.ResponseTime = DateTime.Now;
                LoggerAccessor.Instance.LoggerMonitor?.Invoke(model.ToJson());
            }
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            return await Monitor<TRequest, TResponse>(request, context, continuation);
        }

        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream,
            ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            return await Monitor<TRequest, TResponse>(requestStream, context, continuation);
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            await Monitor<TRequest, TResponse>(request, context, continuation,responseStream);
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            await Monitor<TRequest, TResponse>(requestStream, context, continuation, responseStream);
        }
    }
}
