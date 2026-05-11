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

# Non-root user — create after COPYs so we can set ownership on writable dirs
RUN mkdir -p /app/logs \
 && addgroup --system appgroup \
 && adduser  --system --ingroup appgroup appuser \
 && chown -R appuser:appgroup /app/logs

USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "MediMind.API.dll"]
