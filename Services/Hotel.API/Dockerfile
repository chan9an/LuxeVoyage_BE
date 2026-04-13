FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Shared/Shared/Shared.csproj Shared/Shared/
COPY Services/Hotel.API/Hotel.API.csproj Services/Hotel.API/
RUN dotnet restore Services/Hotel.API/Hotel.API.csproj

COPY Shared/ Shared/
COPY Services/Hotel.API/ Services/Hotel.API/
RUN dotnet publish Services/Hotel.API/Hotel.API.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Hotel.API.dll"]
