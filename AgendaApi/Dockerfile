# Usa a imagem do SDK para compilar o projeto
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copia os arquivos de projeto e restaura as dependências
COPY *.sln .
COPY AgendaApi/*.csproj ./AgendaApi/
RUN dotnet restore ./AgendaApi/AgendaApi.csproj

# Copia o resto do código e publica a aplicação
COPY AgendaApi/. ./AgendaApi/
WORKDIR /app/AgendaApi
RUN dotnet publish -c Release -o out

# Usa a imagem de runtime para executar a aplicação
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/AgendaApi/out .

# Configura a porta que o Render espera (8080) e inicia a aplicação
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgendaApi.dll"]