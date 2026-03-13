FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
ARG BUILD_DATE
WORKDIR /src
COPY ["MnestixApi/MnestixApi.csproj", "MnestixApi/"]
COPY ["MnestixCore/MnestixCore.csproj", "MnestixCore/"]
RUN dotnet restore "./MnestixApi/MnestixApi.csproj" -a $TARGETARCH
COPY . .
WORKDIR "/src/MnestixApi"
RUN dotnet build "./MnestixApi.csproj" -c $BUILD_CONFIGURATION -o /app/build -a $TARGETARCH

# tests
WORKDIR /src
# arm64 not supported by ephemeral-mongo
# https://github.com/asimmon/ephemeral-mongo/issues/3
RUN ARCH=$(uname -m); \
    if [ "$ARCH" = "x86_64" ]; then \
        # manually install libssl1.1 in the Docker container to ensure that our tests can run successfully.
        # The .NET SDK 8.0 Docker image has been updated to Debian 12 (Bookworm), which no longer includes libssl1.1 by default.
        # libssl1.1 is required for EphemeralMongo tests to run in the Docker container, as it is needed to establish secure communication with MongoDB.
        # Therefore, we need to manually install libssl1.1 in the Docker container to ensure that our tests can run successfully.
        wget http://security.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb; \
        dpkg -i libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb && rm libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb; \
        dotnet test --logger "trx;LogFileName=TestResults.trx"; \
    fi

WORKDIR "/src/MnestixApi"
RUN dotnet publish "./MnestixApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM base AS final
ARG BUILD_DATE
ENV BuildDate=$BUILD_DATE
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MnestixApi.dll"]