FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
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
RUN dotnet test --logger "trx;LogFileName=TestResults.trx"

WORKDIR "/src/MnestixApi"
RUN dotnet publish "./MnestixApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM base AS final
ARG BUILD_DATE
ENV BuildDate=$BUILD_DATE
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MnestixApi.dll"]