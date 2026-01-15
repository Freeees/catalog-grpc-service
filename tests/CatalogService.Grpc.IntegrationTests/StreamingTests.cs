using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;

using ContractsV1 = CatalogService.Grpc.Contracts.V1;

namespace CatalogService.Grpc.IntegrationTests;

public sealed class StreamingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StreamingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WatchCatalog_server_streaming_returns_requested_number_of_updates()
    {
        using GrpcChannel channel = GrpcTestClientFactory.CreateChannel(_factory);
        var client = new ContractsV1.CatalogApi.CatalogApiClient(channel);

        using var call = client.WatchCatalog(new ContractsV1.WatchCatalogRequest
        {
            IntervalMs = 10,
            MaxUpdates = 5
        });

        var updates = new List<ContractsV1.CatalogUpdate>();

        await foreach (var item in call.ResponseStream.ReadAllAsync())
        {
            updates.Add(item);
        }

        updates.Should().HaveCount(5);
        updates.Select(u => u.Sequence).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task UploadCatalogEvents_client_streaming_returns_received_count()
    {
        using GrpcChannel channel = GrpcTestClientFactory.CreateChannel(_factory);
        var client = new ContractsV1.CatalogApi.CatalogApiClient(channel);

        using var call = client.UploadCatalogEvents();

        const int n = 7;
        for (var i = 0; i < n; i++)
        {
            await call.RequestStream.WriteAsync(new ContractsV1.CatalogEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                Type = "TestEvent",
                PayloadJson = $$"""{"i": {{i}}}""",
                ClientTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        await call.RequestStream.CompleteAsync();

        var summary = await call.ResponseAsync;
        summary.ReceivedCount.Should().Be(n);
        summary.ServerTimeUnixMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Chat_bidirectional_streaming_echoes_messages()
    {
        using GrpcChannel channel = GrpcTestClientFactory.CreateChannel(_factory);
        var client = new ContractsV1.CatalogApi.CatalogApiClient(channel);

        using var call = client.Chat();

        // reading the answers in parallel
        var received = new List<ContractsV1.ChatMessage>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var msg in call.ResponseStream.ReadAllAsync())
            {
                received.Add(msg);
            }
        });

        // sending 3 messages
        var sent = new[]
        {
            "hi 1",
            "hi 2",
            "hi 3"
        };

        foreach (var text in sent)
        {
            await call.RequestStream.WriteAsync(new ContractsV1.ChatMessage
            {
                Sender = "client",
                Text = text,
                TimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        await call.RequestStream.CompleteAsync();
        await readTask;

        received.Should().HaveCount(3);
        received.All(m => m.Sender == "server").Should().BeTrue();
        received.Select(m => m.Text).Should().Contain(text => text.Contains("echo"));
    }

    [Fact]
    public async Task WatchCatalog_can_be_cancelled()
    {
        using GrpcChannel channel = GrpcTestClientFactory.CreateChannel(_factory);
        var client = new ContractsV1.CatalogApi.CatalogApiClient(channel);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // sending CancellationToken to CallOptions
        using var call = client.WatchCatalog(
            new ContractsV1.WatchCatalogRequest { IntervalMs = 100, MaxUpdates = 1000 },
            cancellationToken: cts.Token);

        // reading until cancelled
        Func<Task> act = async () =>
        {
            await foreach (var _ in call.ResponseStream.ReadAllAsync(cts.Token))
            {
                // no-op
            }
        };

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => 
                ex.StatusCode.Equals(StatusCode.Cancelled) || 
                ex.StatusCode.Equals(StatusCode.DeadlineExceeded
            ));
    }
}
