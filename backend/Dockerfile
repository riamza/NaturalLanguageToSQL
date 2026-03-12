# Stage 1: Build logic
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["backend/NlToSqlAssistant.sln", "./"]
COPY ["backend/Core/Core.csproj", "Core/"]
COPY ["backend/Application/Application.csproj", "Application/"]
COPY ["backend/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["backend/Api/Api.csproj", "Api/"]

# Restore dependencies
RUN dotnet restore "Api/Api.csproj"

# Copy the remaining source code
COPY backend/ .

# Build the application
WORKDIR "/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Api.dll"]