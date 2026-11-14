# Built from the repository root so project references resolve:
#   docker build -f src/Saas.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the project files alone first, so the layer cache survives
# source edits and only invalidates when dependencies actually change.
COPY Directory.Build.props global.json ./
COPY src/Saas.Core/Saas.Core.csproj               src/Saas.Core/
COPY src/Saas.Data/Saas.Data.csproj               src/Saas.Data/
COPY src/Saas.Business/Saas.Business.csproj       src/Saas.Business/
COPY src/Saas.Api/Saas.Api.csproj                 src/Saas.Api/
RUN dotnet restore src/Saas.Api/Saas.Api.csproj

COPY src/Saas.Core/     src/Saas.Core/
COPY src/Saas.Data/     src/Saas.Data/
COPY src/Saas.Business/ src/Saas.Business/
COPY src/Saas.Api/      src/Saas.Api/
RUN dotnet publish src/Saas.Api/Saas.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl backs the compose healthcheck; the aspnet image does not ship it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run unprivileged. The image defines this user already.
USER $APP_UID

COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Saas.Api.dll"]
