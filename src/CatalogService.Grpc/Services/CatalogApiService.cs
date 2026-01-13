using Grpc.Core;
using ContractsV1 = CatalogService.Grpc.Contracts.V1;

namespace CatalogService.Grpc.Services;

public sealed class CatalogApiService : ContractsV1.CatalogApi.CatalogApiBase
{
    public override Task<ContractsV1.PingResponse> Ping(ContractsV1.PingRequest request, ServerCallContext context)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return Task.FromResult(new ContractsV1.PingResponse
        {
            Message = $"pong: {request.Message}",
            ServerTimeUnixMs = now
        });
    }

    // Server streaming
    public override async Task WatchCatalog(
        ContractsV1.WatchCatalogRequest request,
        IServerStreamWriter<ContractsV1.CatalogUpdate> responseStream,
        ServerCallContext context)
    {
        var intervalMs = request.IntervalMs <= 0 ? 500 : request.IntervalMs;
        var max = request.MaxUpdates <= 0 ? 10 : request.MaxUpdates;

        for (long i = 1; i <= max; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            await responseStream.WriteAsync(new ContractsV1.CatalogUpdate
            {
                Sequence = i,
                Text = $"Update #{i}",
                ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            await Task.Delay(intervalMs, context.CancellationToken);
        }
    }

    // Client streaming
    public override async Task<ContractsV1.UploadSummary> UploadCatalogEvents(
        IAsyncStreamReader<ContractsV1.CatalogEvent> requestStream,
        ServerCallContext context)
    {
        var count = 0;

        await foreach (var ev in requestStream.ReadAllAsync(context.CancellationToken))
        {
            count++;
        }

        return new ContractsV1.UploadSummary
        {
            ReceivedCount = count,
            ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    // Bidirectional streaming
    public override async Task Chat(
        IAsyncStreamReader<ContractsV1.ChatMessage> requestStream,
        IServerStreamWriter<ContractsV1.ChatMessage> responseStream,
        ServerCallContext context)
    {
        await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
        {
            // Demo unswer: adding prefix and sending back
            await responseStream.WriteAsync(new ContractsV1.ChatMessage
            {
                Sender = "server",
                Text = $"echo: {msg.Sender}: {msg.Text}",
                TimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }
}
