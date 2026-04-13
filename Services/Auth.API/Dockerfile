FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy shared project first so it can be restored alongside the API
COPY Shared/Shared/Shared.csproj Shared/Shared/
COPY Services/Auth.API/Auth.API.csproj Services/Auth.API/
RUN dotnet restore Services/Auth.API/Auth.API.csproj

# Copy full source and build
COPY Shared/ Shared/
COPY Services/Auth.API/ Services/Auth.API/
RUN dotnet publish Services/Auth.API/Auth.API.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# .env is NOT copied — credentials come from environment variables set in Azure
ENTRYPOINT ["dotnet", "Auth.API.dll"]
