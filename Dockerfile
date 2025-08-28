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

# Build and publish the application
RUN dotnet publish "LocationDeco.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install SQLite and Entity Framework tools
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Create database directory with proper permissions
RUN mkdir -p /app/Data && \
    chmod 755 /app/Data && \
    chown -R app:app /app/Data

# Create a non-root user for security
RUN groupadd -r app && useradd -r -g app app
RUN chown -R app:app /app

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/Data/LocationDeco.db"

# Switch to non-root user
USER app

# Expose the port
EXPOSE 5000

# Health check (optional but recommended)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "LocationDeco.API.dll"]