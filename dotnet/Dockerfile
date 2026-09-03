FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY UpsChecklist.slnx ./
COPY UpsChecklist.Core ./UpsChecklist.Core
COPY UpsChecklist.Web ./UpsChecklist.Web
RUN dotnet publish UpsChecklist.Web/UpsChecklist.Web.csproj -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "UpsChecklist.Web.dll"]
