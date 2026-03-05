FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["AimOnlineRaceStudio.Api.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS run
WORKDIR /app
COPY --from=build /app .
# CSV volume mount will be at /data/csv
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AimOnlineRaceStudio.Api.dll"]
