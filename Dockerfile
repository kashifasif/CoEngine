# ─────────────────────────────────────────────────────────────────────────────
# Stage 1 — Build & Publish
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node.js & npm for Tailwind CSS CLI during build
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Copy project files first for optimal layer caching
COPY src/CoEngine.Shared/CoEngine.Shared.csproj       src/CoEngine.Shared/
COPY src/CoEngine.Client/CoEngine.Client.csproj       src/CoEngine.Client/
COPY src/CoEngine.Api/CoEngine.Api.csproj             src/CoEngine.Api/

# Restore dependencies (cached unless .csproj files change)
RUN dotnet restore src/CoEngine.Api/CoEngine.Api.csproj

# Copy full source
COPY src/ src/

# Publish API (includes Blazor WASM output as static files)
RUN dotnet publish src/CoEngine.Api/CoEngine.Api.csproj \
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

ENTRYPOINT ["dotnet", "CoEngine.Api.dll"]
