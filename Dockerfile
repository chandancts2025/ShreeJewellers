# ============================================================
# Shree Jewellers — Web API Dockerfile for Render / Cloud Deployment
# Multi-stage build: SDK build -> Publish -> ASP.NET Runtime
# ============================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for optimized layer caching
COPY ["Domain/ShreeJewellers.Domain.csproj", "Domain/"]
COPY ["Application/ShreeJewellers.Application.csproj", "Application/"]
COPY ["Infrastructure/ShreeJewellers.Infrastructure.csproj", "Infrastructure/"]
COPY ["API/ShreeJewellers.API.csproj", "API/"]

# Restore NuGet dependencies
RUN dotnet restore "API/ShreeJewellers.API.csproj"

# Copy full solution source code
COPY . .

# Build and publish release output
WORKDIR "/src/API"
RUN dotnet publish "ShreeJewellers.API.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health check probing
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy build artifacts
COPY --from=build /app/publish .

# Create directory for file storage
RUN mkdir -p /app/secure-uploads /app/logs

# Runtime environment settings
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PORT=8080

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:${PORT:-8080}/health/live || exit 1

ENTRYPOINT ["dotnet", "ShreeJewellers.API.dll"]
