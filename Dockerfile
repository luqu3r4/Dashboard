# Build a nivel de solucion (para que anadir proyectos de modulo mas
# adelante no requiera reescribir este Dockerfile).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DashBoard.sln ./
COPY src/DashBoard.Core/DashBoard.Core.csproj src/DashBoard.Core/
RUN dotnet restore DashBoard.sln

COPY src/ src/
RUN dotnet publish src/DashBoard.Core/DashBoard.Core.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DashBoard.Core.dll"]
