# syntax=docker/dockerfile:1
# Multi-architecture support: linux/amd64, linux/arm64

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
LABEL org.opencontainers.image.licenses="AGPL-3.0-or-later" \
  org.opencontainers.image.source="https://github.com/beetnode/izBoard" \
  org.opencontainers.image.title="izBoard" \
  org.opencontainers.image.description="Application for managing the dashboards for e-paper display"
RUN apt-get update && apt-get install -y --no-install-recommends \
  ca-certificates \
  fonts-liberation \
  && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

# Angular output is architecture independent, so it is built once on the build platform
FROM --platform=$BUILDPLATFORM node:22-bookworm-slim AS frontend
WORKDIR /src/EPaperDashboard/frontend
COPY EPaperDashboard/frontend/package.json EPaperDashboard/frontend/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY EPaperDashboard/frontend/ ./
RUN npm run build

# Cross-compiles on the build platform instead of running the SDK under emulation; the output is
# framework dependent and has no app host, so only the runtime identifier depends on the target
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release

WORKDIR /src/EPaperDashboard

# Map Docker build args to .NET Runtime Identifier (RID)
RUN case "$TARGETARCH" in \
  amd64) RID=linux-x64 ;; \
  arm64) RID=linux-arm64 ;; \
  arm*) RID=linux-arm ;; \
  *) RID=linux-$TARGETARCH ;; \
  esac && \
  echo "$RID" > /tmp/rid

# Restore from the project file alone so source changes reuse the restored packages
COPY EPaperDashboard/EPaperDashboard.csproj ./
RUN dotnet restore -r "$(cat /tmp/rid)"

COPY . /src/
COPY --from=frontend /src/EPaperDashboard/wwwroot ./wwwroot
ARG VERSION=1.0.0
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish -r "$(cat /tmp/rid)" --no-restore \
  /p:SkipAngularBuild=true \
  /p:UseAppHost=false \
  /p:Version=$VERSION \
  /p:DebugType=none \
  /p:DebugSymbols=false

# Copy license files to publish directory
RUN cp /src/LICENSE /app/publish/ && \
  cp /src/NOTICE /app/publish/ && \
  cp /src/THIRD-PARTY-NOTICES.txt /app/publish/

FROM base AS runtime
# Rendering component image built with this application, pinned by digest in CI builds
ARG COMPONENT_IMAGE
WORKDIR /app
EXPOSE 8128 8129
COPY --from=publish /app/publish .
ENV RENDERING_COMPONENT_IMAGE=$COMPONENT_IMAGE
RUN mkdir -p /data/DataProtection-Keys /data/components /run/izboard-rendering && \
  chown -R app:app /data /run/izboard-rendering && chmod -R 770 /data /run/izboard-rendering
CMD ["dotnet", "EPaperDashboard.dll"]

# Home Assistant add-on: stays root because Supervisor mounts /data owned by root
FROM runtime AS addon
ARG ADDON_VERSION=latest
LABEL io.hass.name="izBoard" \
  io.hass.description="E-Paper Dashboard for Home Assistant" \
  io.hass.type="addon" \
  io.hass.version="${ADDON_VERSION}"

# Standalone image; must stay the last stage so it remains the default target
FROM runtime AS final
USER app
