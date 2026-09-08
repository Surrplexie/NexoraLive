# NL fork image for example-game — Phase T0 reference (hello-fork stand-in).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY samples/configs/example-game.nle /samples/configs/example-game.nle
ENV NL_FORK_STATUS=/data/fork-status.json
ENV NL_FORK_GAME=hello
EXPOSE 27000
ENTRYPOINT ["dotnet", "NL.Fork.Runtime.dll", "--config", "/samples/configs/example-game.nle"]
