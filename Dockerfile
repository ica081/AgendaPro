FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.sln .
COPY AgendaApi/*.csproj ./AgendaApi/
RUN dotnet restore ./AgendaApi/AgendaApi.csproj

COPY AgendaApi/. ./AgendaApi/
WORKDIR /app/AgendaApi
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/AgendaApi/out .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgendaApi.dll"]