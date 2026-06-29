FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS publish
WORKDIR /src

ARG PROJECT_DIRECTORY="Management"

COPY . .

RUN dotnet restore $PROJECT_DIRECTORY
RUN dotnet publish -c Release -o ./publish --no-restore $PROJECT_DIRECTORY

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Install Docker, Docker Compose, and AWS CLI
RUN apk add --update docker openrc docker-compose \
    && rc-update add docker boot

# Install dependencies and AWS CLI v2
RUN apk add --no-cache \
        curl \
        unzip \
        python3 \
        py3-pip \
        groff \
        less \
    && curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip" \
    && unzip awscliv2.zip \
    && ./aws/install \
    && rm -rf awscliv2.zip aws \
    && apk del curl unzip

EXPOSE 8080
EXPOSE 443

COPY --from=publish /src/publish/* .

ENTRYPOINT ["dotnet", "Management.dll"]