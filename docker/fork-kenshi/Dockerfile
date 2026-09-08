# NL Kenshi fork (Phase T1) — C# sidecar + optional licensed game tree.
# Sidecar is the CI/dogfood default. Mount a licensed Kenshi tree at /game for community MP.
#   docker run -v /path/to/Kenshi:/game -e NL_KENSHI_DEDICATED=1 nl-fork-kenshi:latest
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app
RUN dotnet publish integrations/kenshi/mod/NL.Kenshi.Bridge.csproj -c Release -o /mod

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY --from=build /mod /opt/nl-kenshi-mod/Assemblies
COPY integrations/kenshi/mod/About /opt/nl-kenshi-mod/About
COPY docker/fork-kenshi/entrypoint.sh /entrypoint.sh
COPY samples/configs/kenshi.nle /samples/configs/kenshi.nle
COPY samples/fork/kenshi.mods.json /samples/fork/kenshi.mods.json
RUN chmod +x /entrypoint.sh
ENV NL_FORK_GAME=kenshi
ENV NL_FORK_MODS=/samples/fork/kenshi.mods.json
ENV NL_FORK_STATUS=/data/fork-status.json
ENV NL_FORK_CONNECT_PORT=23386
VOLUME ["/data", "/game"]
EXPOSE 23386
ENTRYPOINT ["/entrypoint.sh"]
