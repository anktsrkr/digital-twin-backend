# Multi-stage Dockerfile for .NET 10 Web API
# Compatible with SnapDeploy, Out Plane, Caasify, Railway, and Docker environments

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ResumeAssistant.Core/ResumeAssistant.Core.csproj", "ResumeAssistant.Core/"]
COPY ["src/ResumeAssistant.Api/ResumeAssistant.Api.csproj", "ResumeAssistant.Api/"]
RUN dotnet restore "ResumeAssistant.Api/ResumeAssistant.Api.csproj"

# Copy source code and build
COPY src/ .
WORKDIR "/src/ResumeAssistant.Api"
RUN dotnet publish "ResumeAssistant.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Lean production runtime stage (<100MB)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ResumeAssistant.Api.dll"]
