FROM mcr.microsoft.com/dotnet/sdk:9.0

# Install system dependencies and mono runtime
RUN apt-get update && apt-get install -y make mono-runtime

# Install .NET 8 runtime
RUN wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet \
    && rm -f dotnet-install.sh

# Verify installations
RUN dotnet --list-runtimes && mono --version

WORKDIR /repo
COPY . .

CMD make pack