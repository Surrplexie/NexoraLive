# NL Minecraft Paper server fork (Phase P) — real Java dedicated + NL bridge plugin.
# Build plugin: mvn -f integrations/minecraft/paper/pom.xml package
# Build image:  docker build -f docker/fork-minecraft-paper/Dockerfile -t nl-fork-minecraft-paper:latest .

FROM maven:3.9-eclipse-temurin-21 AS plugin-build
WORKDIR /src
COPY integrations/minecraft/paper/pom.xml integrations/minecraft/paper/pom.xml
COPY integrations/minecraft/paper/src integrations/minecraft/paper/src
RUN mvn -f integrations/minecraft/paper/pom.xml -q package

FROM itzg/minecraft-server:java21
ENV TYPE=PAPER
ENV EULA=TRUE
ENV MEMORY=1G
ENV VERSION=1.21.1
ENV PLUGINS=/plugins
ENV NL_FORK_STATUS=/data/fork-status.json
COPY --from=plugin-build /src/integrations/minecraft/paper/target/nl-bridge-1.0.0.jar /plugins/NLBridge.jar
COPY samples/configs/minecraft.nle /data/rules.nle
VOLUME ["/data"]
EXPOSE 25565
