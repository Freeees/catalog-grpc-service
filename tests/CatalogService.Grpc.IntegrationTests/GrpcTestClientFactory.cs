using CatalogService.Grpc;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CatalogService.Grpc.IntegrationTests;

public static class GrpcTestClientFactory
{
    public static GrpcChannel CreateChannel(WebApplicationFactory<GrpcEntryPoint> factory)
    {
        var httpClient = factory.CreateDefaultClient(new ResponseVersionHandler());

        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
    }

    private sealed class ResponseVersionHandler : DelegatingHandler
    {
        public ResponseVersionHandler() : base(new HttpClientHandler()) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Version = new Version(2, 0);
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
