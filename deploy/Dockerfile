# Everdue — one image, one process: API, SPA, occurrence engine and digest.
# Build from the repository root:  docker build -f deploy/Dockerfile -t everdue .

# --- 1. Client -------------------------------------------------------------------------------
FROM node:24-bookworm-slim AS client
WORKDIR /src/client

COPY src/client/package.json src/client/package-lock.json ./
RUN npm ci

COPY src/client/ ./
RUN npm run build

# --- 2. Server -------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Server/Everdue.Server.csproj src/Server/
RUN dotnet restore src/Server/Everdue.Server.csproj

COPY src/Server/ src/Server/
COPY --from=client /src/client/dist/ src/Server/wwwroot/

# Framework-dependent: the runtime is already in the final image, so single-file self-containment
# would only make the layer bigger. The single-file build is for the copy-one-binary install.
RUN dotnet publish src/Server/Everdue.Server.csproj \
      -c Release \
      -o /app \
      -p:SkipClientBuild=true

# --- 3. Runtime ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The tz database is not optional here: every period boundary is computed in the tenant's IANA zone.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=server /app ./

# One volume holds the entire state: the SQLite file and its WAL sidecars.
ENV DataDir=/data \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
VOLUME ["/data"]
EXPOSE 8080

RUN useradd --system --uid 10001 everdue && mkdir -p /data && chown -R everdue /data /app
USER everdue

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["/app/Everdue.Server"]
