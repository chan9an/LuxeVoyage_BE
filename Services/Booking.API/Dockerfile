FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Shared/Shared/Shared.csproj Shared/Shared/
COPY Services/Booking.API/Booking.API.csproj Services/Booking.API/
RUN dotnet restore Services/Booking.API/Booking.API.csproj

COPY Shared/ Shared/
COPY Services/Booking.API/ Services/Booking.API/
RUN dotnet publish Services/Booking.API/Booking.API.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Booking.API.dll"]
