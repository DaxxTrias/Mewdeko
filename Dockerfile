# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH

WORKDIR /source

COPY src/Mewdeko/Mewdeko.csproj ./src/Mewdeko/
COPY src/MewdekoSourceGen/MewdekoSourceGen.csproj ./src/MewdekoSourceGen/

RUN dotnet restore ./src/Mewdeko/Mewdeko.csproj -a $TARGETARCH

COPY src/ ./src/

WORKDIR /source/src/Mewdeko
RUN dotnet publish ./Mewdeko.csproj \
    -c Release \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish ./

RUN mkdir -p /app/data /app/logs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_URLS=http://+:5001

EXPOSE 5001

ENTRYPOINT ["dotnet", "Mewdeko.dll"]
