# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ./src/CatalogService.Grpc/CatalogService.Grpc.csproj
RUN dotnet publish ./src/CatalogService.Grpc/CatalogService.Grpc.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 5144
EXPOSE 7090

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CatalogService.Grpc.dll"]
