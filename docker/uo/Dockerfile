FROM debian:12-slim AS builder

ARG DOTNET_VERSION=10.0.203
ARG MODERNUO_COMMIT=7c9215d97

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates git libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Install .NET SDK
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
    --version ${DOTNET_VERSION} \
    --install-dir /usr/local/dotnet \
    && ln -s /usr/local/dotnet/dotnet /usr/local/bin/dotnet

ENV DOTNET_ROOT=/usr/local/dotnet
ENV PATH="$PATH:/usr/local/dotnet"

# Clone ModernUO at the exact commit used on Proxmox
RUN git clone https://github.com/modernuo/ModernUO.git /build/modernuo \
    && cd /build/modernuo \
    && git checkout ${MODERNUO_COMMIT}

# Build ModernUO core — scripts are compiled at runtime by ModernUO,
# not at image build time. Custom .cs files come in via bind mount.
WORKDIR /build/modernuo
RUN dotnet publish -c Release -o /modernuo/Distribution

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM debian:12-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Install .NET runtime only (not full SDK)
ARG DOTNET_VERSION=10.0.203
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
        --version ${DOTNET_VERSION} \
        --runtime dotnet \
        --install-dir /usr/local/dotnet \
    && ln -s /usr/local/dotnet/dotnet /usr/local/bin/dotnet \
    && apt-get purge -y curl && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_ROOT=/usr/local/dotnet
ENV PATH="$PATH:/usr/local/dotnet"
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0

# Copy built Distribution from builder stage
COPY --from=builder /modernuo/Distribution /modernuo

# Create runtime directories
RUN mkdir -p \
    /var/lib/uo/modernuo/Saves \
    /var/lib/uo/modernuo/Backups \
    /var/lib/uo/modernuo/Archives \
    /var/lib/uo/modernuo/temp \
    /var/lib/uo/client-data/classic-client

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

WORKDIR /modernuo
EXPOSE 2593

ENTRYPOINT ["/entrypoint.sh"]
