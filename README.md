# catalog-grpc-service
![CI](https://github.com/<OWNER>/catalog-grpc-service/actions/workflows/ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-blue)

Portfolio project demonstrating **gRPC communication between services** on **.NET 8** with:
- gRPC **server + client**
- **Unary**, **Server Streaming**, **Client Streaming**, **Bidirectional Streaming**
- **Protobuf versioning** (v1 + v2 running side-by-side)
- **Retry + Timeout** on the client (HttpClientFactory + Polly)
- **Integration tests** (in-memory host, unary + streaming + cancellation)
- **Docker** support for a reproducible demo environment

---

## Tech Stack

- .NET 8 (SDK pinned via `global.json`)
- ASP.NET Core gRPC (Kestrel, HTTP/2)
- Protobuf (`/proto` as a single source of truth)
- Client: `Grpc.Net.ClientFactory` + Polly (retry/timeout)
- Tests: xUnit + `Microsoft.AspNetCore.Mvc.Testing`

---

## Repository Structure

```
proto/
  catalog/v1/catalog.proto
  catalog/v2/catalog.proto
src/
  CatalogService.Domain/			# empty
  CatalogService.Application/		# empty
  CatalogService.Infrastructure/	# empty
  CatalogService.Grpc/				# gRPC server (v1 + v2)
  CatalogService.Client/			# console client (v1 + v2)
tests/
  CatalogService.Grpc.IntegrationTests/
Dockerfile
docker-compose.yml
```

---

## gRPC API

### v1
- `Ping` (unary)
- `WatchCatalog` (server streaming)
- `UploadCatalogEvents` (client streaming)
- `Chat` (bidirectional streaming)

### v2
- Same endpoints, but updated contract (example: `correlation_id`, `code`, `version` fields)
- Runs **in parallel** with v1 (backward compatible approach)

---

## How to Run (Local)

### 1) Trust dev certificate (once per machine)

```powershell
dotnet dev-certs https --trust
```

### 2) Run the server

```powershell
dotnet run --project .\src\CatalogService.Grpc\CatalogService.Grpc.csproj
```

Local server listens on:
- http://localhost:5144
- https://localhost:7090

### 3) Run the client (in another terminal)

```powershell
dotnet run --project .\src\CatalogService.Client\CatalogService.Client.csproj
```

Client reads server address from:
`src/CatalogService.Client/appsettings.json`

---

## Docker

> In Docker this project runs **HTTP-only** by design to keep the setup simple and fully reproducible
> (no dev certificates inside containers). In real production setups TLS is typically terminated by a
> reverse proxy / ingress (Nginx, Traefik, Kubernetes Ingress, etc).

### Prerequisites
- Docker Desktop

### Build and run

```powershell
docker compose build
docker compose up
```

Docker server listens on:
- http://localhost:5144

### Run the client against Docker

Update `src/CatalogService.Client/appsettings.json` temporarily:

```json
{
  "Grpc": {
    "CatalogServiceAddress": "http://localhost:5144"
  }
}
```

(For local non-Docker run you can switch it back to `https://localhost:7090`.)

---

## Tests

Run integration tests (unary + streaming + cancellation):

```powershell
dotnet test .\tests\CatalogService.Grpc.IntegrationTests\CatalogService.Grpc.IntegrationTests.csproj
```

---

## What This Project Demonstrates

- Designing gRPC services with all streaming modes
- Building a robust .NET gRPC client (DI, retries, timeouts)
- Versioning Protobuf contracts (v1/v2) without breaking clients
- Writing integration tests for gRPC using an in-memory host
- Production-minded approach: HTTP/2, cancellation, clean repo structure
- Dockerized server for reproducible demos

---

## Notes

- `proto/` is the contract source of truth (both server and client generate code from it).
- The server hosts **both** v1 and v2 services simultaneously.
- Docker uses `ASPNETCORE_ENVIRONMENT=Docker` and `src/CatalogService.Grpc/appsettings.Docker.json` (HTTP-only).
