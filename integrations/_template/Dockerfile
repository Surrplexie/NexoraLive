# NL fork image for {{GAME_ID}} — Phase T0 template.
# Default: reuse hello-fork runtime as a sidecar stand-in until the real dedicated server is wired.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY samples/configs/{{GAME_ID}}.nle /samples/configs/{{GAME_ID}}.nle
ENV NL_FORK_STATUS=/data/fork-status.json
ENV NL_FORK_GAME=hello
EXPOSE {{PORT}}
ENTRYPOINT ["dotnet", "NL.Fork.Runtime.dll", "--config", "/samples/configs/{{GAME_ID}}.nle"]
