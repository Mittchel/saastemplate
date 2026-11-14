# Built from the repository root so project references resolve:
#   docker build -f src/Saas.Auth.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/Saas.Core/Saas.Core.csproj               src/Saas.Core/
COPY src/Saas.Data/Saas.Data.csproj               src/Saas.Data/
COPY src/Saas.Business/Saas.Business.csproj       src/Saas.Business/
COPY src/Saas.Auth.Api/Saas.Auth.Api.csproj       src/Saas.Auth.Api/
RUN dotnet restore src/Saas.Auth.Api/Saas.Auth.Api.csproj

COPY src/Saas.Core/     src/Saas.Core/
COPY src/Saas.Data/     src/Saas.Data/
COPY src/Saas.Business/ src/Saas.Business/
COPY src/Saas.Auth.Api/ src/Saas.Auth.Api/
RUN dotnet publish src/Saas.Auth.Api/Saas.Auth.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl backs the compose healthcheck; the aspnet image does not ship it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Saas.Auth.Api.dll"]
