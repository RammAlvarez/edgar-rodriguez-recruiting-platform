# platform-api: the API and its migration bundle in one image (ARCHITECTURE §5: migrations run from the API image before the
# new revision takes traffic). Built from the repository root:
#   docker build -f services/platform-api/Dockerfile -t platform-api .
# Run the API:        docker run platform-api                      (env vars: .env.example)
# Run the migrations: docker run --entrypoint ./efbundle platform-api  (needs MIGRATIONS_DATABASE_URL)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY .config/ .config/
COPY services/platform-api/ services/platform-api/
WORKDIR /src/services/platform-api
RUN dotnet tool restore \
    && dotnet publish src/Platform.Api/Platform.Api.csproj -c Release -o /out/api \
    && dotnet ef migrations bundle --project src/Platform.Infrastructure --startup-project src/Platform.Infrastructure \
        --configuration Release --output /out/api/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080
# The image's unprivileged user.
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.Api.dll"]
