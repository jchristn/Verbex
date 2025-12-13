FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY Verbex/Verbex.csproj Verbex/
COPY Verbex.Server/Verbex.Server.csproj Verbex.Server/
RUN dotnet restore Verbex.Server/Verbex.Server.csproj

# Copy source code and build
COPY Verbex/ Verbex/
COPY Verbex.Server/ Verbex.Server/
RUN dotnet publish Verbex.Server/Verbex.Server.csproj -c Release -o /app/publish /p:GeneratePackageOnBuild=false /p:ErrorOnDuplicatePublishOutputFiles=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Create data and logs directories
RUN mkdir -p /app/data /app/logs

# Install diagnostic tools
RUN apt-get update && apt-get install -y iputils-ping traceroute net-tools curl wget dnsutils iproute2 vim file && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENTRYPOINT ["dotnet", "Verbex.Server.dll"]
