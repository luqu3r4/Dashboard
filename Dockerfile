FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/DashBoard.Web/DashBoard.Web.csproj src/DashBoard.Web/
COPY src/DashBoard.Core/DashBoard.Core.csproj src/DashBoard.Core/
COPY src/Modules/DashBoard.Modules.Sistema/DashBoard.Modules.Sistema.csproj src/Modules/DashBoard.Modules.Sistema/
COPY src/Modules/DashBoard.Modules.Salud/DashBoard.Modules.Salud.csproj src/Modules/DashBoard.Modules.Salud/
RUN dotnet restore src/DashBoard.Web/DashBoard.Web.csproj

COPY src/ src/
RUN dotnet publish src/DashBoard.Web/DashBoard.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# El contenedor monta el sistema de archivos del host en solo lectura; ejecutarlo sin root
# limita lo que puede leer. /keys guarda las claves de Data Protection y debe ser escribible.
RUN mkdir -p /keys && chown $APP_UID /keys
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DashBoard.Web.dll"]
