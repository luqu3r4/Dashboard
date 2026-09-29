FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/DashBoard.Web/DashBoard.Web.csproj src/DashBoard.Web/
COPY src/DashBoard.Core/DashBoard.Core.csproj src/DashBoard.Core/
RUN dotnet restore src/DashBoard.Web/DashBoard.Web.csproj

COPY src/ src/
RUN dotnet publish src/DashBoard.Web/DashBoard.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DashBoard.Web.dll"]
