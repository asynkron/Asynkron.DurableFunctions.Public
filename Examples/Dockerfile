# 🐳 Multi-stage Dockerfile for the sample API

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/Asynkron.DurableFunctions/Asynkron.DurableFunctions.csproj", "src/Asynkron.DurableFunctions/"]
COPY ["examples/Asynkron.DurableFunctions.Examples.csproj", "examples/"] 
RUN dotnet restore "examples/Asynkron.DurableFunctions.Examples.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/examples"
RUN dotnet build "Asynkron.DurableFunctions.Examples.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Asynkron.DurableFunctions.Examples.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Use PostgreSqlExample.cs instead of the complex FullFeaturedWebAppExample.cs
ENTRYPOINT ["dotnet", "Asynkron.DurableFunctions.Examples.dll", "postgresql"]