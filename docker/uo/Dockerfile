FROM debian:12-slim

ARG DOTNET_VERSION=10.0.203

# Install dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Install .NET SDK
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
    --version ${DOTNET_VERSION} \
    --install-dir /usr/local/dotnet \
    && ln -s /usr/local/dotnet/dotnet /usr/local/bin/dotnet

ENV DOTNET_ROOT=/usr/local/dotnet
ENV PATH="$PATH:/usr/local/dotnet"
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0

WORKDIR /modernuo

# Copy pre-built ModernUO release or build context
# The server binary and Data/ are bind-mounted from the host at runtime,
# so this layer just ensures the image is runnable.
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 2593

ENTRYPOINT ["/entrypoint.sh"]
