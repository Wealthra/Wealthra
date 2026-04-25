# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["Wealthra.Api/Wealthra.Api.csproj", "Wealthra.Api/"]
COPY ["Wealthra.Application/Wealthra.Application.csproj", "Wealthra.Application/"]
COPY ["Wealthra.Domain/Wealthra.Domain.csproj", "Wealthra.Domain/"]
COPY ["Wealthra.Infrastructure/Wealthra.Infrastructure.csproj", "Wealthra.Infrastructure/"]
RUN dotnet restore "Wealthra.Api/Wealthra.Api.csproj"

# Copy the rest of the code and build
COPY . .
WORKDIR "/src/Wealthra.Api"
RUN dotnet publish "Wealthra.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    fontconfig \
    libfreetype6 \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Expose port 8080 (default for .NET 8+)
EXPOSE 8080

ENTRYPOINT ["dotnet", "Wealthra.Api.dll"]