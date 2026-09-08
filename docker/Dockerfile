FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NL.SessionHost.Web/NL.SessionHost.Web.csproj
RUN dotnet publish src/NL.SessionHost.Web/NL.SessionHost.Web.csproj -c Release -o /out/session
RUN dotnet restore src/NL.Moderation.Web/NL.Moderation.Web.csproj
RUN dotnet publish src/NL.Moderation.Web/NL.Moderation.Web.csproj -c Release -o /out/moderation

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS session-host
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -fsSL https://download.docker.com/linux/static/stable/x86_64/docker-27.3.1.tgz \
    | tar xz -C /tmp \
    && mv /tmp/docker/docker /usr/local/bin/docker \
    && rm -rf /tmp/docker \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/session .
COPY samples/configs /app/samples/configs
COPY samples/events /app/samples/events
COPY samples/dogfood /app/samples/dogfood
COPY samples/identity /app/samples/identity
COPY samples/fork /app/samples/fork
ENV NL_BIND=0.0.0.0
ENV NL_HTTP_PORT=27020
ENV NL_WS_PORT=27021
ENV NL_DATA_ROOT=/data
EXPOSE 27020 27021
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:27020/health || exit 1
ENTRYPOINT ["dotnet", "NL.SessionHost.Web.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS moderation
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/moderation .
ENV NL_BIND=0.0.0.0
ENV NL_MOD_HTTP_PORT=27030
ENV NL_DATA_ROOT=/data
EXPOSE 27030
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://127.0.0.1:27030/health || exit 1
ENTRYPOINT ["dotnet", "NL.Moderation.Web.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS server
WORKDIR /app
COPY --from=build /out/server .
COPY samples/configs /app/samples/configs
COPY samples/events /app/samples/events
ENV NL_DATA_ROOT=/data
ENTRYPOINT ["dotnet", "NL.Server.dll"]
