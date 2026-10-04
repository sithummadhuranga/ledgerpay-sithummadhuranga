# Two images from one build: the API, and the database tool that creates the schema and the seed data.
# Build from the backend folder:  docker build --target api -t ledgerpay-api backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project files first, so the restore is cached until a package changes.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/LedgerPay.Domain/LedgerPay.Domain.csproj src/LedgerPay.Domain/
COPY src/LedgerPay.Application/LedgerPay.Application.csproj src/LedgerPay.Application/
COPY src/LedgerPay.Infrastructure/LedgerPay.Infrastructure.csproj src/LedgerPay.Infrastructure/
COPY src/LedgerPay.Api/LedgerPay.Api.csproj src/LedgerPay.Api/
COPY tools/LedgerPay.DbTool/LedgerPay.DbTool.csproj tools/LedgerPay.DbTool/
RUN dotnet restore src/LedgerPay.Api/LedgerPay.Api.csproj \
    && dotnet restore tools/LedgerPay.DbTool/LedgerPay.DbTool.csproj

COPY src src
COPY tools tools
RUN dotnet publish src/LedgerPay.Api/LedgerPay.Api.csproj -c Release -o /out/api --no-restore \
    && dotnet publish tools/LedgerPay.DbTool/LedgerPay.DbTool.csproj -c Release -o /out/dbtool --no-restore

# The API runs as the non-root user the base image provides. It reads its settings from environment variables.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "LedgerPay.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS dbtool
WORKDIR /app
COPY --from=build /out/dbtool .
USER $APP_UID
ENTRYPOINT ["dotnet", "LedgerPay.DbTool.dll"]
CMD ["setup"]
