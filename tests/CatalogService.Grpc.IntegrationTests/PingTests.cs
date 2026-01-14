using FluentAssertions;
using Xunit;
using Grpc.Net.Client;
using ContractsV1 = CatalogService.Grpc.Contracts.V1;
using ContractsV2 = CatalogService.Grpc.Contracts.V2;


namespace CatalogService.Grpc.IntegrationTests;

public sealed class PingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ping_v1_returns_pong()
    {
        using GrpcChannel channel = GrpcTestClientFactory.CreateChannel(_factory);
        var client = new ContractsV1.CatalogApi.CatalogApiClient(channel);

        var reply = await client.PingAsync(new ContractsV1.PingRequest { Message = "test" });

        reply.Message.Should().Contain("pong");
        reply.ServerTimeUnixMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Ping_v2_returns_pong_and_correlation_id()
    {
        using GrpcChannel channel = GrpcTestClientFactory.CreateChannel(_factory);
        var client = new ContractsV2.CatalogApi.CatalogApiClient(channel);

        var correlationId = Guid.NewGuid().ToString("N");
        var reply = await client.PingAsync(new ContractsV2.PingRequest
        {
            Message = "test",
            CorrelationId = correlationId
        });

        reply.Message.Should().Contain("pong(v2)");
        reply.CorrelationId.Should().Be(correlationId);
        reply.Code.Should().Be(0);
        reply.ServerTimeUnixMs.Should().BeGreaterThan(0);
    }
}
