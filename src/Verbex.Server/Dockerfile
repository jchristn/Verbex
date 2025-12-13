# Use the official .NET 8.0 runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Verbex.Server/Verbex.Server.csproj", "Verbex.Server/"]
COPY ["Verbex/Verbex.csproj", "Verbex/"]

# Restore dependencies
RUN dotnet restore "Verbex.Server/Verbex.Server.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/Verbex.Server"
RUN dotnet build "Verbex.Server.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Verbex.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - create the runtime image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create data directory for persistent storage
RUN mkdir -p /app/data && chmod 755 /app/data

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV VERBEX_STORAGE_MODE=Hybrid
ENV VERBEX_STORAGE_DIRECTORY=/app/data
ENV VERBEX_HOT_CACHE_SIZE=10000
ENV VERBEX_DOCUMENT_CACHE_SIZE=1000
ENV VERBEX_MAX_SEARCH_RESULTS=100
ENV VERBEX_FLUSH_INTERVAL=60

# Set the entry point
ENTRYPOINT ["dotnet", "Verbex.Server.dll"]