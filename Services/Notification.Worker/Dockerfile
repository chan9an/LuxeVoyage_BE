FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Shared/Shared/Shared.csproj Shared/Shared/
COPY Services/Notification.Worker/Notification.Worker.csproj Services/Notification.Worker/
RUN dotnet restore Services/Notification.Worker/Notification.Worker.csproj

COPY Shared/ Shared/
COPY Services/Notification.Worker/ Services/Notification.Worker/
RUN dotnet publish Services/Notification.Worker/Notification.Worker.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Notification.Worker.dll"]
