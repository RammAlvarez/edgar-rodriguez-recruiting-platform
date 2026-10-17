# The three static apps behind Caddy (same-VM variant; ARCHITECTURE §5). Built from the repository root:
#   docker build -f deploy/apps.Dockerfile -t platform-apps .
# Each app reads /config.json at start (API URL and Firebase web config): it is mounted per environment, not built in.
FROM node:24-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY . .
RUN npx ng build admin && npx ng build candidates && npx ng build clients

FROM caddy:2.10-alpine
COPY --from=build /src/dist/admin/browser /srv/admin
COPY --from=build /src/dist/candidates/browser /srv/candidates
COPY --from=build /src/dist/clients/browser /srv/clients
# The built-in config.json is the local one: the real one is mounted at /appconfig/<app>.json.
RUN rm -f /srv/admin/config.json /srv/candidates/config.json /srv/clients/config.json
COPY deploy/Caddyfile /etc/caddy/Caddyfile
