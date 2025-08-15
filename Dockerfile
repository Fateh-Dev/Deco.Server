# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["LocationDeco.API.csproj", "./"]
RUN dotnet restore "LocationDeco.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src"
RUN dotnet build "LocationDeco.API.csproj" -c Release -o /app/build

# Publish the app
FROM build AS publish
RUN dotnet publish "LocationDeco.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Install SQLite3 for EF Core migrations
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Create and set permissions for the SQLite database directory
RUN mkdir -p /app/Data && \
    chmod 777 /app/Data

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the database migrations and then start the application
ENTRYPOINT ["sh", "-c", "dotnet ef database update --no-build && dotnet LocationDeco.API.dll"]
