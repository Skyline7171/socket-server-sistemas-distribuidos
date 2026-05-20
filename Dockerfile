# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Busca cualquier .csproj en las subcarpetas y lo copia a la raíz de compilación
COPY **/*.csproj ./
RUN dotnet restore

# Copia todo el resto del código fuente
COPY . ./

# Compila buscando el proyecto de forma automática
RUN dotnet publish -c Release -o /app

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# IMPORTANTE: Asegúrate de que este nombre coincida con el nombre de tu proyecto
ENTRYPOINT ["dotnet", "socket-server-sistemas-distribuidos.dll"]