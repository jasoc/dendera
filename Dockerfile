# Dendera — one image, one container: the Angular app is built here and served as static files
# by Dendera.Api itself (wwwroot), next to the /api endpoints. SQLite lives in /data (a volume,
# see docker-compose.yml), so the image holds no personal data. Lives at the repo root, next to
# its build context; .dockerignore keeps everything but src/ and global.json out.

# Every base image is pinned by digest, so a rebuild produces the image that was reviewed and
# a moved tag can't change what ships; bumping one is a reviewed change:
#   docker buildx imagetools inspect <image>:<tag> --format '{{.Manifest.Digest}}'

# ---- SPA build ----
# Node pinned to src/frontend/.node-version.
FROM node:24.22.3-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5 AS spa-build
WORKDIR /src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ .
# angular.json writes the production build to ../backend/Dendera.Api/wwwroot.
RUN npm run build

# ---- server build ----
# Satisfies /global.json (10.0.300, rollForward latestFeature). Published in one step.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS server-build
WORKDIR /src
COPY global.json ./
COPY src/backend/ src/backend/
# Reported by /api/health. A CI build can pass git describe (1.3.0, 1.2.0-5-gabc1234); a local
# build (prod-test.sh, `up --build`) is just "dev". Not called VERSION: a build arg is an
# environment variable in RUN, MSBuild reads those as properties, and $(Version)=dev fails restore.
ARG DENDERA_VERSION=dev
RUN dotnet publish src/backend/Dendera.Api/Dendera.Api.csproj -c Release -o /app/publish \
    -p:InformationalVersion="$DENDERA_VERSION"

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:12b1135f5f8b3c0a214f2a680ff2033dff4f684f445d6c697d9524ceeb303586 AS runtime
WORKDIR /app
COPY --from=server-build /app/publish ./
COPY --from=spa-build /src/backend/Dendera.Api/wwwroot ./wwwroot
# Owned by the image's non-root user, so a fresh named volume mounted here inherits it.
RUN mkdir /data && chown "$APP_UID" /data
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Dendera="Data Source=/data/dendera.db"
VOLUME /data

ENTRYPOINT ["dotnet", "Dendera.Api.dll"]
