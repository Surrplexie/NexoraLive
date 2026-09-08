# NL Minecraft fork sidecar (Phase P) — C# runtime with minecraft.nle event vocabulary.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY samples/configs/minecraft.nle /samples/configs/minecraft.nle
COPY samples/fork/minecraft.mods.json /samples/fork/minecraft.mods.json
ENV NL_FORK_GAME=minecraft
ENV NL_FORK_MODS=/samples/fork/minecraft.mods.json
ENV NL_FORK_STATUS=/data/fork-status.json
ENTRYPOINT ["dotnet", "NL.Fork.Runtime.dll", "--game", "minecraft"]
