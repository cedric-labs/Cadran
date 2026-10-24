# syntax=docker/dockerfile:1
# ------------------------------------------------------------------------------
# Official ASP.NET Core 9 runtime base image (Debian bookworm-slim with glibc)
# Native support for SQLiteMC dynamic engine via SQLitePCLRaw
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Create persistent storage directories and assign ownership to unprivileged "app" user
RUN mkdir -p /app/data /app/backups && \
    chown -R app:app /app

# Copy published artifacts from scripts/publish-docker.ps1
COPY --chown=app:app ./publish/Cadran-Docker/app /app

# Environment variables for containerized execution
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/cadran.db;Mode=ReadWriteCreate;Foreign Keys=True;" \
    Server__Port=8080 \
    Server__AllowNetworkAccess=true \
    Server__AutoLaunchBrowser=false

# Expose HTTP port
EXPOSE 8080

# Declared persistent storage volumes
VOLUME ["/app/data", "/app/backups"]

# Run under secure non-root user
USER app

ENTRYPOINT ["dotnet", "Cadran.dll"]
