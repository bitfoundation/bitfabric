# bit Fabric's performance and accessibility scanners (bitfabric-internal/specs/scanners.md): Lighthouse, with Debian's
# Chromium, behind a guard that keeps Chrome on the public internet (src/guard.ts). The Node image is pinned by version
# and digest and Lighthouse by package-lock.json; Chromium is Debian's current build when the image is built, so each
# deployment gets its security fixes, and the service reports its version on /health.
FROM node:26.12.1-trixie-slim@sha256:94816674c62774da0d099a947bf4613b9e1972a2733abd0d0cd80e795f8411d0

# tini as PID 1 reaps what a killed Chrome leaves behind; the fonts draw the scripts customers' pages use, Persian and
# Arabic among them, so the contrast and layout checks measure real text.
RUN apt-get update \
    && apt-get install -y --no-install-recommends chromium tini ca-certificates fonts-liberation fonts-noto-core fonts-noto-color-emoji \
    && rm -rf /var/lib/apt/lists/*

ENV NODE_ENV=production \
    CHROME_PATH=/usr/bin/chromium \
    PORT=3000 \
    HOME=/tmp \
    XDG_CONFIG_HOME=/tmp/.config \
    XDG_CACHE_HOME=/tmp/.cache

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --omit=dev --no-audit --no-fund && npm cache clean --force

COPY src ./src

# The node image's own unprivileged user; the file system stays the image's, and Chrome's profiles go to /tmp.
USER node

EXPOSE 3000

HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD ["node", "-e", "fetch('http://127.0.0.1:3000/alive').then(r => process.exit(r.ok ? 0 : 1), () => process.exit(1))"]

ENTRYPOINT ["/usr/bin/tini", "--"]
CMD ["node", "src/server.ts"]
