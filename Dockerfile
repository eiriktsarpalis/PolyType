FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine

RUN apk add git make

WORKDIR /repo
COPY . .

CMD make pack
