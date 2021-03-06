﻿using System;
using System.Collections;
using System.Reflection;
using Grpc.Extension.Common;
using Grpc.Extension.BaseService.Model;

namespace Grpc.Extension.BaseService
{
    /// <summary>
    /// GrpcServiceExtension
    /// </summary>
    internal static class GrpcServiceExtension
    {
        /// <summary>
        /// 生成Grpc元数据信息(1.19以前可以反射处理)
        /// </summary>
        /// <param name="callHandlers"></param>
        public static void BuildMeta(IDictionary callHandlers)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            //获取Grpc元数据信息
            foreach (DictionaryEntry callHandler in callHandlers)
            {
                //反射获取Handlers
                var hFiled = callHandler.Value.GetFieldValue<Delegate>("handler", bindingFlags);
                var handler = hFiled.Item1;
                var types = hFiled.Item2.DeclaringType.GenericTypeArguments;
                MetaModel.Methods.Add((new MetaMethodModel
                {
                    FullName = callHandler.Key.ToString(),
                    RequestType = types[0],
                    ResponseType = types[1],
                    Handler = handler
                }));                
            }
        }
    }
}
