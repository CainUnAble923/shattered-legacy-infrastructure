# The pinned ModernUO commit. Declared before the first FROM so both stages can read it: the
# builder checks it out, and the runtime stage records it for entrypoint.sh's banner (cc-P37).
ARG MODERNUO_COMMIT=d4531cd94b739613155225c234900de9f47d2c88

FROM debian:12-slim AS builder

ARG DOTNET_VERSION=10.0.203
ARG MODERNUO_COMMIT

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

# The runtime stage's native libraries, for the test host. build.sh runs the shard's tests in
# THIS stage, and without them the first gump a test sends (every gump is deflate-packed) throws
# in LibDeflateBinding's constructor and again in its finalizer, killing the test host; archive
# code (ZstdNet) and password hashing (Argon2.Bindings) fail the same way. The list is exactly the
# runtime stage's, less what line 6 already has, so the builder never has a library the running
# server lacks. Keep the two in step. A layer of its own, after the clone, so that changing it
# never invalidates the .NET SDK or clone layers above. See notes/gate-integrity.md part B.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libdeflate-dev libzstd-dev libargon2-1 \
    && ln -sf /usr/lib/x86_64-linux-gnu/libargon2.so.1 /usr/local/lib/libargon2.so \
    && ln -sf /usr/lib/x86_64-linux-gnu/libargon2.so.1 /usr/local/lib/libargon2.so.1 \
    && ldconfig \
    && rm -rf /var/lib/apt/lists/*

# Stage canonical custom scripts. apply-patches.sh routes additive scripts and
# full-file replacements to their verified locations in the pinned source tree.
COPY server/customizations/ /customizations/

# Stage the shard's own regression tests. apply-patches.sh mirrors them into
# Projects/UOContent.Tests/Tests/ by path. They are compiled by the publish below because
# UOContent.Tests is in ModernUO.slnx, so a test that does not compile fails this build.
# Running them needs relaxed seccomp for io_uring, which `docker build` cannot grant:
# docker/uo/build.sh is the gate that executes them. See notes/s4-test-route.md.
COPY server/tests/ /tests/

# The guild hall decor design file, for GuildHallDecorVerification: it compares the server's generated table with
# this file itself, so an edit to the JSON without regenerating the table fails the build (cc-P459). Only .cs files
# reach the test project, so the test reads it from here.
COPY design/guild-hall-decor/templates.json /design/guild-hall-decor/templates.json

# Copy serialization migration JSONs (needed for PetMimic VNContent generation)
COPY server/migrations/ /build/modernuo/Projects/UOContent/Migrations/

# cc-P631: the New Haven townsfolk's dialogue table (ClusterFTalkRules). Into the builder's Distribution/Data, where the
# tests read the shipped bytes; the runtime stage copies that Data folder whole, so the server reads the same file at
# /modernuo/Data/ShatteredLegacy/. Only .cs files reach the projects, so a data file needs its own line.
COPY server/data/townsfolk-talk.json /build/modernuo/Distribution/Data/ShatteredLegacy/townsfolk-talk.json
# cc-P676 (SoS swap S3): Trammel's regions on the SoS land, read by RegionJsonSerializer-shard-maps.patch in place of
# pinned's Trammel regions (made by shard-migration notes/cc-P676-tools/gen_regions.py).
COPY server/data/regions-trammel.json /build/modernuo/Distribution/Data/ShatteredLegacy/regions-trammel.json
# cc-P686 (SoS swap S13): Felucca's regions on the same SoS land, in place of pinned's Felucca regions (the same
# patch; made by shard-migration notes/cc-P686-tools/gen_felucca.py).
COPY server/data/regions-felucca.json /build/modernuo/Distribution/Data/ShatteredLegacy/regions-felucca.json
# cc-P679 (SoS swap S4): the Trammel and Felucca teleporters on the SoS land, read by GenTeleporter-sos-teleporters.patch
# (SosTeleporters) in place of pinned's (made by shard-migration notes/cc-P679-tools/tele_fix.py).
COPY server/data/teleporters-sos.json /build/modernuo/Distribution/Data/ShatteredLegacy/teleporters-sos.json
# cc-P679 (SoS swap S4): the staff go-menu for Trammel and Felucca, the author's (Zigholtul's Siege of Sosaria
# Data/Locations/trammel.xml, converted by shard-migration notes/cc-P679-tools/golocations.py), read by
# GoLocations-sos-go-menu.patch in place of pinned's Data/Locations/trammel.json and felucca.json.
COPY server/data/Locations/ /build/modernuo/Distribution/Data/ShatteredLegacy/Locations/
# cc-P680 (SoS swap S5): the Trammel and Felucca decoration on the SoS land, read by Decorate-sos-decoration.patch in
# place of pinned's Data/Decoration/{Britannia,Trammel,Felucca} (made by shard-migration notes/cc-P680-tools/deco.py).
COPY server/data/Decoration/ /build/modernuo/Distribution/Data/ShatteredLegacy/Decoration/
# cc-P684 (SoS swap S11): pinned's treasure.cfg decided for the SoS land, with spots on the new land, in its place
# (TreasureMap's level-0 and classic spots, TreasureMapProtection's no-housing squares; made by shard-migration
# notes/cc-P684-tools/treasure.py).
COPY server/data/treasure.cfg /build/modernuo/Distribution/Data/treasure.cfg
# cc-P682 (SoS swap S10): the people of the author's new towns and keeps and the wild on his new land, in pinned's
# spawn format beside pinned's own (Data/Spawns/sos/{towns,keeps,wild}), built by the stock [GenerateSpawners (made by
# shard-migration notes/cc-P682-tools/build_spawns.py).
COPY server/data/Spawns/ /build/modernuo/Distribution/Data/Spawns/

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

# -- Runtime stage --------------------------------------------------------------
FROM debian:12-slim

# entrypoint.sh prints this in its banner. It used to hardcode the hash, which was wrong as soon as
# a build used another commit (cc-P30 run 1: an upstream image announcing 7c9215d97).
ARG MODERNUO_COMMIT
ENV SL_MODERNUO_COMMIT=${MODERNUO_COMMIT}

# The builder stage installs this list's native libraries too, for the test host. Keep the two in step.
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

# F-15 (cc-P22): the patch notes ship with the build they describe. ShardVersion.cs reads both at server start.
COPY CHANGELOG.md VERSION /modernuo/Data/ShatteredLegacy/

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
