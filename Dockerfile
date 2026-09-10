FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1

# Install system dependencies and mono runtime
RUN apt-get update && apt-get install -y make mono-runtime

# Install runtimes for the existing test and tool target frameworks
RUN wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet --skip-non-versioned-files \
    && ./dotnet-install.sh --channel 9.0 --runtime dotnet --install-dir /usr/share/dotnet --skip-non-versioned-files \
    && ./dotnet-install.sh --channel 10.0 --runtime dotnet --install-dir /usr/share/dotnet --skip-non-versioned-files \
    && rm -f dotnet-install.sh

# Verify installations
RUN dotnet --list-runtimes && mono --version

WORKDIR /repo
COPY . .

CMD make pack