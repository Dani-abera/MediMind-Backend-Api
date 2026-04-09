# ─── API Dockerfile ─────────────────────────────────────────────────────────────
# Multi-stage build for lean production image

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files first (layer caching for NuGet restore)
COPY MediMind.sln .
COPY src/MediMind.Domain/MediMind.Domain.csproj src/MediMind.Domain/
COPY src/MediMind.Application/MediMind.Application.csproj src/MediMind.Application/
COPY src/MediMind.Infrastructure/MediMind.Infrastructure.csproj src/MediMind.Infrastructure/
COPY src/MediMind.API/MediMind.API.csproj src/MediMind.API/

# Restore NuGet packages
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build src/MediMind.API/MediMind.API.csproj -c Release --no-restore
RUN dotnet publish src/MediMind.API/MediMind.API.csproj -c Release -o /app/publish --no-build

# ─── Runtime Stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: run as non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "MediMind.API.dll"]
