# NL RimWorld fork (Phase T1) — C# sidecar + optional licensed Together dedicated.
# Sidecar is the CI/dogfood default. Mount a licensed RimWorld tree at /game to run dedicated.
#   docker run -v /path/to/RimWorld:/game -e NL_RIMWORLD_DEDICATED=1 nl-fork-rimworld:latest
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app
RUN dotnet publish integrations/rimworld/mod/NL.RimWorld.Bridge.csproj -c Release -o /mod

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY --from=build /mod /opt/nl-rimworld-mod/Assemblies
COPY integrations/rimworld/mod/About /opt/nl-rimworld-mod/About
COPY docker/fork-rimworld/entrypoint.sh /entrypoint.sh
COPY samples/configs/rimworld.nle /samples/configs/rimworld.nle
COPY samples/fork/rimworld.mods.json /samples/fork/rimworld.mods.json
RUN chmod +x /entrypoint.sh
ENV NL_FORK_GAME=rimworld
ENV NL_FORK_MODS=/samples/fork/rimworld.mods.json
ENV NL_FORK_STATUS=/data/fork-status.json
ENV NL_FORK_CONNECT_PORT=25555
VOLUME ["/data", "/game"]
EXPOSE 25555
ENTRYPOINT ["/entrypoint.sh"]
