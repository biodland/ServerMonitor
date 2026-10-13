# ServerMonitor - container image (monolith, see ../DESIGN.md).
# Needs host IPMI/DMI and lm-sensors access at runtime: run privileged with
# /sys + /proc mounted (see docker-compose.yaml), or prefer the systemd unit.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Apps/ServerMonitor/ServerMonitor.csproj src/Apps/ServerMonitor/
RUN dotnet restore src/Apps/ServerMonitor/ServerMonitor.csproj
COPY src/Apps/ServerMonitor/ src/Apps/ServerMonitor/
RUN dotnet publish src/Apps/ServerMonitor/ServerMonitor.csproj \
      -c Release -o /app/publish --no-restore \
      -p:UseAppHost=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
      && apt-get install -y --no-install-recommends ipmitool iproute2 lm-sensors curl \
      && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 1080
ENTRYPOINT ["./servermonitor"]
