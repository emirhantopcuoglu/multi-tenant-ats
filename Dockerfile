FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the source tree; tests are excluded by .dockerignore.
# Restoring by project (not solution) avoids pulling test-only packages.
COPY src/ src/
RUN dotnet restore src/Ats.Api/Ats.Api.csproj

RUN dotnet publish src/Ats.Api/Ats.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# 'app' is the non-root user pre-created in Microsoft's ASP.NET Core images (uid 1654).
# Running unprivileged is a container security baseline.
USER app
COPY --from=build --chown=app /app/publish .

# ASP.NET Core 8+ defaults to port 8080 when not running as root.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Ats.Api.dll"]
