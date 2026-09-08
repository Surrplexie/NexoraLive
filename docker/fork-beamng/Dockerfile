# NL BeamNG fork sidecar (Phase P) — pairs with BeamNG client + NL_BeamNGBridge mod on host.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY samples/configs/beamng.nle /samples/configs/beamng.nle
COPY samples/fork/beamng.mods.json /samples/fork/beamng.mods.json
ENV NL_FORK_GAME=beamng
ENV NL_FORK_MODS=/samples/fork/beamng.mods.json
ENV NL_FORK_STATUS=/data/fork-status.json
ENTRYPOINT ["dotnet", "NL.Fork.Runtime.dll", "--game", "beamng"]
