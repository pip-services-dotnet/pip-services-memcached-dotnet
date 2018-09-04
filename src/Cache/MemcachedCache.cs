﻿using System;
using System.Threading.Tasks;

using Enyim.Caching;
using Enyim.Caching.Configuration;

using PipServices.Commons.Config;
using PipServices.Commons.Errors;
using PipServices.Commons.Refer;
using PipServices.Components.Auth;
using PipServices.Components.Cache;
using PipServices.Components.Connect;

namespace PipServices.Memcached.Cache
{
    public class MemcachedCache : AbstractCache
    {
        private ConnectionResolver _connectionResolver = new ConnectionResolver();
        private CredentialResolver _credentialResolver = new CredentialResolver();
        private MemcachedClient _client = null;

        public MemcachedCache()
        {
        }

        public override void Configure(ConfigParams config)
        {
            base.Configure(config);

            _connectionResolver.Configure(config);
            _credentialResolver.Configure(config);
        }

        public override void SetReferences(IReferences references)
        {
            _connectionResolver.SetReferences(references);
            _credentialResolver.SetReferences(references);
        }

        public override bool IsOpen()
        {
            return _client != null;
        }

        public override async Task OpenAsync(string correlationId)
        {
            var connections = await _connectionResolver.ResolveAllAsync(correlationId);
            if (connections.Count == 0)
                throw new ConfigException(correlationId, "NO_CONNECTION", "Connection is not configured");

            var options = new MemcachedClientConfiguration();

            foreach (var connection in connections)
            {
                var uri = connection.Uri;

                if (!string.IsNullOrEmpty(uri))
                {
                    options.AddServer(uri, 11211);
                }
                else 
                {
                    var host = connection.Host ?? "localhost";
                    var port = connection.Port != 0 ? connection.Port : 11211;

                    options.AddServer(host, port);
                }
            }

            _client = new MemcachedClient(null, options);
        }

        public override async Task CloseAsync(string correlationId)
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }

            await Task.Delay(0);
        }

        private void CheckOpened(string correlationId)
        {
            if (!IsOpen())
                throw new InvalidStateException(correlationId, "NOT_OPENED", "Connection is not opened");
        }

        public override async Task<T> RetrieveAsync<T>(string correlationId, string key)
        {
            CheckOpened(correlationId);

            return await _client.GetAsync<T>(key);
        }

        public override async Task<T> StoreAsync<T>(string correlationId, string key, T value, long timeout)
        {
            CheckOpened(correlationId);

            timeout = timeout > 0 ? timeout : Timeout;

            var result = await _client.StoreAsync(Enyim.Caching.Memcached.StoreMode.Set, key, value, TimeSpan.FromMilliseconds(timeout));

            return result ? value : default(T);
        }

        public override async Task RemoveAsync(string correlationId, string key)
        {
            CheckOpened(correlationId);

            await _client.RemoveAsync(key);
        }
    }
}
