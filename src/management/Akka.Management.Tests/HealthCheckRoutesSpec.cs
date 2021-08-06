using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Extensions;
using Akka.Management.Dsl;
using Akka.Util;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Abstractions;
using HttpRequest = Akka.Http.Dsl.Model.HttpRequest;


namespace Akka.Management.Tests
{
    public class HealthCheckRoutesSpec : TestKit.Xunit2.TestKit
    {
        private static Config Config = Config.Empty
            .WithFallback(Akka.Http.Dsl.Http.DefaultConfig())
            .WithFallback(AkkaManagementProvider.DefaultConfiguration());
        
        public HealthCheckRoutesSpec(ITestOutputHelper helper) : base(Config, nameof(HealthCheckRoutesSpec), helper)
        { }
        
        [Fact(DisplayName = "Health check /ready endpoint should return 200 for right")]
        public async Task HealthCheckReadyReturn200ForRight()
        {
            var result = (RouteResult.Complete)
                await TestRoutes().Concat()(Get("/ready"));
            result.Response.Status.Should().Be((int) HttpStatusCode.OK);
        }
        
        [Fact(DisplayName = "Health check /ready endpoint should return 500 for left")]
        public async Task HealthCheckReadyReturn500ForLeft()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(
                    readyResultValue: Task.FromResult((Either<string, Done>)new Left<string, Done>("com.someclass.MyCheck"))).Concat()(Get("/ready"));
            result.Response.Status.Should().Be((int) HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Not Healthy: com.someclass.MyCheck");
        }
        
        [Fact(DisplayName = "Health check /ready endpoint should return 500 for fail")]
        public async Task HealthCheckReadyReturn500ForFail()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(
                    readyResultValue: Task.FromException<Either<string, Done>>(new Exception("darn it"))).Concat()(Get("/ready"));
            result.Response.Status.Should().Be((int) HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Health Check Failed: darn it");
        }
        
        [Fact(DisplayName = "Health check /alive endpoint should return 200 for right")]
        public async Task HealthCheckAliveReturn200ForRight()
        {
            var result = (RouteResult.Complete)
                await TestRoutes().Concat()(Get("/alive"));
            result.Response.Status.Should().Be((int) HttpStatusCode.OK);
        }
        
        [Fact(DisplayName = "Health check /alive endpoint should return 500 for left")]
        public async Task HealthCheckAliveReturn500ForLeft()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(
                    aliveResultValue: Task.FromResult(
                        (Either<string, Done>) new Left<string, Done>("com.someclass.MyCheck"))).Concat()(Get("/alive"));
            result.Response.Status.Should().Be((int) HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Not Healthy: com.someclass.MyCheck");
        }
        
        [Fact(DisplayName = "Health check /alive endpoint should return 500 for fail")]
        public async Task HealthCheckAliveReturn500ForFail()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(aliveResultValue: Task.FromException<Either<string, Done>>(new Exception("darn it"))).Concat()
                    (Get("/alive"));
            result.Response.Status.Should().Be((int) HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Health Check Failed: darn it");
        }

        private RequestContext Get(string route)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = route;
            context.Request.Body = new MemoryStream();
            context.Request.Protocol = "HTTP/1.1";

            var request = HttpRequest.Create(context.Request);
            
            return new RequestContext(request, Sys);
        }
        
        private Route[] TestRoutes(
            Task<Either<string, Done>> readyResultValue = null, 
            Task<Either<string, Done>> aliveResultValue = null)
        {
            return new TestHealthCheckRoutes((ExtendedActorSystem) Sys, readyResultValue, aliveResultValue)
                .Routes(new ManagementRouteProviderSettingsImpl(new Uri("http://whocares"), false));
        }

        private class TestHealthCheckRoutes : HealthCheckRoutes
        {
            internal override HealthChecks HealthChecks { get; }

            public TestHealthCheckRoutes(
                ExtendedActorSystem system,
                Task<Either<string, Done>> readyResultValue, 
                Task<Either<string, Done>> aliveResultValue) : base(system)
            {
                HealthChecks = new TestHealthChecks(readyResultValue, aliveResultValue);
            }
        }
        
        private class TestHealthChecks : HealthChecks
        {
            private readonly Task<Either<string, Done>> _readyResultValue;
            private readonly Task<Either<string, Done>> _aliveResultValue;

            public TestHealthChecks(
                Task<Either<string, Done>> readyResultValue, 
                Task<Either<string, Done>> aliveResultValue)
            {
                _readyResultValue =
                    readyResultValue ??
                    Task.FromResult((Either<string, Done>)new Right<string, Done>(Done.Instance));
                _aliveResultValue =
                    aliveResultValue ??
                    Task.FromResult((Either<string, Done>)new Right<string, Done>(Done.Instance));
            }

            public override Task<bool> Ready()
                => Task.FromResult(_readyResultValue.Result is Right<string, Done>);

            public override Task<Either<string, Done>> ReadyResult()
                => _readyResultValue;

            public override Task<bool> Alive()
                => Task.FromResult(_aliveResultValue.Result is Right<string, Done>);

            public override Task<Either<string, Done>> AliveResult()
                => _aliveResultValue;
        }
    }
}