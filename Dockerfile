# ─────────────────────────────────────────────────────────────────────────────
# Stage 1 — Build & Publish
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for optimal layer caching
COPY src/SpecPlatform.Shared/SpecPlatform.Shared.csproj       src/SpecPlatform.Shared/
COPY src/SpecPlatform.Client/SpecPlatform.Client.csproj       src/SpecPlatform.Client/
COPY src/SpecPlatform.Api/SpecPlatform.Api.csproj             src/SpecPlatform.Api/

# Restore dependencies (cached unless .csproj files change)
RUN dotnet restore src/SpecPlatform.Api/SpecPlatform.Api.csproj

# Copy full source
COPY src/ src/

# Publish API (includes Blazor WASM output as static files)
RUN dotnet publish src/SpecPlatform.Api/SpecPlatform.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─────────────────────────────────────────────────────────────────────────────
# Stage 2 — Runtime
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for HEALTHCHECK
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published output from build stage
COPY --from=build /app/publish .

# App runs on port 8080 inside the container (Azure App Service default)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# Health check — verifies the API is alive
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "SpecPlatform.Api.dll"]
