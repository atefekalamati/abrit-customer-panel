FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/AbrIT.Panel.Web/AbrIT.Panel.Web.csproj src/AbrIT.Panel.Web/
COPY src/AbrIT.Panel.Module/AbrIT.Panel.Module.csproj src/AbrIT.Panel.Module/
COPY src/AbrIT.Panel.Domain/AbrIT.Panel.Domain.csproj src/AbrIT.Panel.Domain/
COPY src/AbrIT.Panel.Nextcloud/AbrIT.Panel.Nextcloud.csproj src/AbrIT.Panel.Nextcloud/
COPY src/AbrIT.Theme/AbrIT.Theme.csproj src/AbrIT.Theme/
RUN dotnet restore src/AbrIT.Panel.Web/AbrIT.Panel.Web.csproj
COPY src/ src/
# Single-source design tokens + font, linked into AbrIT.Theme as embedded assets (D-20).
COPY design-system/ design-system/
RUN dotnet publish src/AbrIT.Panel.Web/AbrIT.Panel.Web.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Pre-create App_Data (volume) and wwwroot (image cache) owned by the non-root user.
RUN mkdir -p /app/App_Data /app/wwwroot && chown -R $APP_UID:$APP_UID /app/App_Data /app/wwwroot
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AbrIT.Panel.Web.dll"]
