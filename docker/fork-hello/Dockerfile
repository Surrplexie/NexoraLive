# NL hello-fork runtime (Phase P) — connects to session bus WebSocket.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY samples/configs/fork-hello.nle /samples/configs/fork-hello.nle
COPY samples/fork/hello-fork.mods.json /samples/fork/hello-fork.mods.json
ENV NL_FORK_MODS=/samples/fork/hello-fork.mods.json
ENV NL_FORK_STATUS=/data/fork-status.json
ENTRYPOINT ["dotnet", "NL.Fork.Runtime.dll"]
