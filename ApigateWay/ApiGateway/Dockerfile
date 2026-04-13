FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ApigateWay/ApiGateway/ApiGateway.csproj ApigateWay/ApiGateway/
RUN dotnet restore ApigateWay/ApiGateway/ApiGateway.csproj

COPY ApigateWay/ApiGateway/ ApigateWay/ApiGateway/
RUN dotnet publish ApigateWay/ApiGateway/ApiGateway.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# ocelot.json must be present at runtime — it's config not secrets so it's safe to copy
COPY ApigateWay/ApiGateway/ocelot.json .

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
