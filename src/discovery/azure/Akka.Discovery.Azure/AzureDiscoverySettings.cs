﻿// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Azure.Data.Tables;
using Azure.Identity;

namespace Akka.Discovery.Azure
{
    public sealed class AzureDiscoverySettings
    {
        public static readonly AzureDiscoverySettings Empty = new AzureDiscoverySettings(
            serviceName: "default",
            hostName: Dns.GetHostName(),
            port: 8558,
            connectionString: "<connection-string>",
            tableName: "akkaclustermembers",
            ttlHeartbeatInterval: TimeSpan.FromMinutes(1),
            staleTtlThreshold: TimeSpan.Zero,
            pruneInterval: TimeSpan.FromHours(1),
            operationTimeout: TimeSpan.FromSeconds(10),
            retryBackoff: TimeSpan.FromMilliseconds(500),
            maximumRetryBackoff: TimeSpan.FromSeconds(5),
            azureTableEndpoint: null,
            azureCredential: null,
            tableClientOptions: null);
        
        public static AzureDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config);

        public static AzureDiscoverySettings Create(Configuration.Config config)
        {
            var cfg = config.GetConfig("akka.discovery.azure");
            var host = cfg.GetString("public-hostname");
            if (string.IsNullOrWhiteSpace(host))
            {
                host = config.GetString("akka.remote.dot-netty.tcp.public-hostname");
                if (string.IsNullOrWhiteSpace(host))
                    host = Dns.GetHostName();
            }
            
            return new AzureDiscoverySettings(
                serviceName: cfg.GetString("service-name"),
                hostName: host,
                port: cfg.GetInt("public-port"),
                connectionString: cfg.GetString("connection-string"),
                tableName: cfg.GetString("table-name"),
                ttlHeartbeatInterval: cfg.GetTimeSpan("ttl-heartbeat-interval"),
                staleTtlThreshold: cfg.GetTimeSpan("stale-ttl-threshold"),
                pruneInterval: cfg.GetTimeSpan("prune-interval"),
                operationTimeout: cfg.GetTimeSpan("operation-timeout"),
                retryBackoff: cfg.GetTimeSpan("retry-backoff"),
                maximumRetryBackoff: cfg.GetTimeSpan("max-retry-backoff"),
                azureTableEndpoint: null,
                azureCredential: null,
                tableClientOptions: null);
        }
        
        private AzureDiscoverySettings(
            string serviceName,
            string hostName,
            int port,
            string connectionString,
            string tableName,
            TimeSpan ttlHeartbeatInterval,
            TimeSpan staleTtlThreshold,
            TimeSpan pruneInterval,
            TimeSpan operationTimeout,
            TimeSpan retryBackoff,
            TimeSpan maximumRetryBackoff,
            Uri azureTableEndpoint,
            DefaultAzureCredential azureCredential,
            TableClientOptions tableClientOptions)
        {
            if (ttlHeartbeatInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttlHeartbeatInterval));
            
            if (pruneInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(pruneInterval));
            
            if (staleTtlThreshold != TimeSpan.Zero && staleTtlThreshold < ttlHeartbeatInterval)
                throw new ArgumentException(
                    $"Must be greater than {nameof(ttlHeartbeatInterval)} if set to non zero",
                    nameof(staleTtlThreshold));

            if(string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException(
                    "Must not be empty or whitespace",
                    nameof(hostName));
            
            if(port < 1 || port > 65535)
                throw new ArgumentException(
                    "Must be greater than zero and less than or equal to 65535",
                    nameof(port));

            if (operationTimeout <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(operationTimeout));
            
            if(retryBackoff <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(retryBackoff));
            
            if(maximumRetryBackoff < retryBackoff)
                throw new ArgumentException($"Must be greater than {nameof(retryBackoff)}", nameof(maximumRetryBackoff));
            
            ServiceName = serviceName;
            HostName = hostName;
            Port = port;
            ConnectionString = connectionString;
            TableName = tableName;
            TtlHeartbeatInterval = ttlHeartbeatInterval;
            StaleTtlThreshold = staleTtlThreshold;
            PruneInterval = pruneInterval;
            OperationTimeout = operationTimeout;
            RetryBackoff = retryBackoff;
            MaximumRetryBackoff = maximumRetryBackoff;
            AzureTableEndpoint = azureTableEndpoint;
            AzureAzureCredential = azureCredential;
            TableClientOptions = tableClientOptions;
        }

        public string ServiceName { get; }
        public string HostName { get; }
        public int Port { get; }
        public string ConnectionString { get; }
        public string TableName { get; }
        public TimeSpan TtlHeartbeatInterval { get; }
        public TimeSpan StaleTtlThreshold { get; }
        public TimeSpan PruneInterval { get; }
        public TimeSpan OperationTimeout { get; }
        public TimeSpan RetryBackoff { get; }
        public TimeSpan MaximumRetryBackoff { get; }
        public Uri AzureTableEndpoint { get; }
        public DefaultAzureCredential AzureAzureCredential { get; }
        public TableClientOptions TableClientOptions { get; }

        public override string ToString()
            => "[AzureDiscoverySettings](" +
               $"{nameof(ServiceName)}:{ServiceName}, " +
               $"{nameof(HostName)}:{HostName}, " +
               $"{nameof(Port)}:{Port}, " +
               $"{nameof(ConnectionString)}:{ConnectionString}, " +
               $"{nameof(TableName)}:{TableName}, " +
               $"{nameof(TtlHeartbeatInterval)}:{TtlHeartbeatInterval}, " +
               $"{nameof(StaleTtlThreshold)}:{StaleTtlThreshold}, " +
               $"{nameof(PruneInterval)}:{PruneInterval}, " +
               $"{nameof(OperationTimeout)}:{OperationTimeout}, " +
               $"{nameof(RetryBackoff)}:{RetryBackoff}, " +
               $"{nameof(MaximumRetryBackoff)}:{MaximumRetryBackoff})";
        
        public TimeSpan EffectiveStaleTtlThreshold
            => StaleTtlThreshold == TimeSpan.Zero ? new TimeSpan(TtlHeartbeatInterval.Ticks * 5)  : StaleTtlThreshold;
        
        public AzureDiscoverySettings WithServiceName(string serviceName)
            => Copy(serviceName: serviceName);
        
        public AzureDiscoverySettings WithPublicHostName(string hostName)
            => Copy(host: hostName);
        
        public AzureDiscoverySettings WithPublicPort(int port)
            => Copy(port: port);
        
        public AzureDiscoverySettings WithConnectionString(string connectionString)
            => Copy(connectionString: connectionString);
        
        public AzureDiscoverySettings WithTableName(string tableName)
            => Copy(tableName: tableName);

        public AzureDiscoverySettings WithTtlHeartbeatInterval(TimeSpan ttlHeartbeatInterval)
            => Copy(ttlHeartbeatInterval: ttlHeartbeatInterval);
        
        public AzureDiscoverySettings WithStaleTtlThreshold(TimeSpan staleTtlThreshold)
            => Copy(staleTtlThreshold: staleTtlThreshold);
        
        public AzureDiscoverySettings WithPruneInterval(TimeSpan pruneInterval)
            => Copy(pruneInterval: pruneInterval);

        public AzureDiscoverySettings WithOperationTimeout(TimeSpan operationTimeout)
            => Copy(operationTimeout: operationTimeout);
        
        public AzureDiscoverySettings WithRetryBackoff(TimeSpan retryBackoff, TimeSpan maximumRetryBackoff)
            => Copy(retryBackoff: retryBackoff, maximumRetryBackoff: maximumRetryBackoff);

        public AzureDiscoverySettings WithAzureCredential(
            Uri azureTableEndpoint,
            DefaultAzureCredential credential,
            TableClientOptions tableClientOptions = null)
            => Copy(
                azureTableEndpoint: azureTableEndpoint,
                credential: credential,
                tableClientOptions: tableClientOptions);
        
        private AzureDiscoverySettings Copy(
            string serviceName = null,
            string host = null,
            int? port = null,
            string connectionString = null,
            string tableName = null,
            TimeSpan? pruneInterval = null,
            TimeSpan? staleTtlThreshold = null,
            TimeSpan? ttlHeartbeatInterval = null,
            TimeSpan? operationTimeout = null,
            TimeSpan? retryBackoff = null,
            TimeSpan? maximumRetryBackoff = null,
            Uri azureTableEndpoint = null,
            DefaultAzureCredential credential = null,
            TableClientOptions tableClientOptions = null)
            => new AzureDiscoverySettings(
                serviceName: serviceName ?? ServiceName,
                hostName: host ?? HostName,
                port: port ?? Port,
                connectionString: connectionString ?? ConnectionString,
                tableName: tableName ?? TableName,
                ttlHeartbeatInterval: ttlHeartbeatInterval ?? TtlHeartbeatInterval,
                staleTtlThreshold: staleTtlThreshold ?? StaleTtlThreshold,
                pruneInterval: pruneInterval ?? PruneInterval,
                operationTimeout: operationTimeout ?? OperationTimeout,
                retryBackoff: retryBackoff ?? RetryBackoff,
                maximumRetryBackoff: maximumRetryBackoff ?? MaximumRetryBackoff,
                azureTableEndpoint: azureTableEndpoint ?? AzureTableEndpoint,
                azureCredential: credential ?? AzureAzureCredential,
                tableClientOptions: tableClientOptions ?? TableClientOptions);
    }
}