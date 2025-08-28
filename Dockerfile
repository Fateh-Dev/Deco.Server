# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["LocationDeco.API.csproj", "./"]
RUN dotnet restore "LocationDeco.API.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "LocationDeco.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install SQLite (if needed at runtime)
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Make sure DB folder exists and is writable
RUN mkdir -p /app/Data && chmod 777 /app/Data

# Environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose the port Koyeb will map
EXPOSE 5000

# Run the app
ENTRYPOINT ["dotnet", "LocationDeco.API.dll"]
