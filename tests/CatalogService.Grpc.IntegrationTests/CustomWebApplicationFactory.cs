using CatalogService.Grpc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CatalogService.Grpc.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<GrpcEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
