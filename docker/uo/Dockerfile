FROM debian:12-slim AS builder

ARG DOTNET_VERSION=10.0.203
ARG MODERNUO_COMMIT=7c9215d97

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates git libicu-dev gcc patch \
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

# Stage canonical custom scripts. apply-patches.sh routes additive scripts and
# full-file replacements to their verified locations in the pinned source tree.
COPY server/customizations/ /customizations/

# Stage the shard's own regression tests. apply-patches.sh mirrors them into
# Projects/UOContent.Tests/Tests/ by path. They are compiled by the publish below because
# UOContent.Tests is in ModernUO.slnx, so a test that does not compile fails this build.
# Running them needs relaxed seccomp for io_uring, which `docker build` cannot grant:
# docker/uo/build.sh is the gate that executes them. See notes/s4-test-route.md.
COPY server/tests/ /tests/

# Copy serialization migration JSONs (needed for PetMimic VNContent generation)
COPY server/migrations/ /build/modernuo/Projects/UOContent/Migrations/

# Copy patch files and apply them
COPY server/patches/ /patches/
COPY docker/uo/apply-patches.sh /apply-patches.sh
RUN sed -i 's/\r$//' /apply-patches.sh \
    && chmod +x /apply-patches.sh \
    && /apply-patches.sh

# Publish full solution
WORKDIR /build/modernuo
RUN dotnet publish ModernUO.slnx -c Release -o /modernuo/Distribution \
    /p:PublishTrimmed=false \
    /p:PublishAot=false \
    /p:PublishSingleFile=false

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM debian:12-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates libicu-dev libdeflate-dev libzstd-dev libargon2-1 \
    && ln -sf /usr/lib/x86_64-linux-gnu/libargon2.so.1 /usr/local/lib/libargon2.so \
    && ln -sf /usr/lib/x86_64-linux-gnu/libargon2.so.1 /usr/local/lib/libargon2.so.1 \
    && ldconfig \
    && rm -rf /var/lib/apt/lists/*

# Reuse the dotnet installation from builder
COPY --from=builder /usr/local/dotnet /usr/local/dotnet
RUN ln -s /usr/local/dotnet/dotnet /usr/local/bin/dotnet

ENV DOTNET_ROOT=/usr/local/dotnet
ENV PATH="$PATH:/usr/local/dotnet"
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0

# Copy built Distribution and Data from builder stage
COPY --from=builder /modernuo/Distribution /modernuo
COPY --from=builder /build/modernuo/Distribution/Data /modernuo/Data

# Create runtime directories
RUN mkdir -p \
    /var/lib/uo/modernuo/Saves \
    /var/lib/uo/modernuo/Backups \
    /var/lib/uo/modernuo/Archives \
    /var/lib/uo/modernuo/temp \
    /var/lib/uo/client-data/classic-client

COPY docker/uo/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

WORKDIR /modernuo
EXPOSE 2593

ENTRYPOINT ["/entrypoint.sh"]
