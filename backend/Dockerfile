# syntax=docker/dockerfile:1
# Build context: the backend/ folder.  docker build -t farmplatform-api backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless project files change).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FarmPlatform.Domain/FarmPlatform.Domain.csproj src/FarmPlatform.Domain/
COPY src/FarmPlatform.Application/FarmPlatform.Application.csproj src/FarmPlatform.Application/
COPY src/FarmPlatform.Infrastructure/FarmPlatform.Infrastructure.csproj src/FarmPlatform.Infrastructure/
COPY src/FarmPlatform.API/FarmPlatform.API.csproj src/FarmPlatform.API/
RUN dotnet restore src/FarmPlatform.API/FarmPlatform.API.csproj

COPY src/ src/
RUN dotnet publish src/FarmPlatform.API/FarmPlatform.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Render terminates TLS and forwards X-Forwarded-For/Proto; trust them so HTTPS-only cookies,
# HSTS and per-IP rate limiting see the real scheme and client address.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "FarmPlatform.API.dll"]
