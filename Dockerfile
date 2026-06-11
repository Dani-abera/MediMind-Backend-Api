# ─── Build Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy project files first — Docker caches this layer until a .csproj changes
COPY MediMind.sln .
COPY src/MediMind.Domain/MediMind.Domain.csproj           src/MediMind.Domain/
COPY src/MediMind.Application/MediMind.Application.csproj src/MediMind.Application/
COPY src/MediMind.Infrastructure/MediMind.Infrastructure.csproj src/MediMind.Infrastructure/
COPY src/MediMind.API/MediMind.API.csproj                 src/MediMind.API/

# Restore only the API and its dependencies (skips test projects)
RUN dotnet restore src/MediMind.API/MediMind.API.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/MediMind.API/MediMind.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Runtime Stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Bake ONNX models into the image (update by replacing files + rebuilding)
COPY src/MediMind.API/models/ ./models/

# Install missing native lib required by Npgsql (Kerberos/GSSAPI)
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*

# Non-root user — pre-create all writable dirs so appuser has access
RUN mkdir -p /app/logs /app/uploads /app/storage/prescriptions \
 && groupadd --system appgroup \
 && useradd  --system --gid appgroup appuser \
 && chown -R appuser:appgroup /app/logs /app/uploads /app/storage

USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "MediMind.API.dll"]
