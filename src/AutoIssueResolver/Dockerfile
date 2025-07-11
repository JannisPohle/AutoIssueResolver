FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
# TODO figure out how to use the $APP_UID for the user, but still be able to read/write to the sqlite database file
USER 1000
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AutoIssueResolver/AutoIssueResolver.csproj", "AutoIssueResolver/"]
RUN dotnet restore "AutoIssueResolver/AutoIssueResolver.csproj"
COPY . .
WORKDIR "/src/AutoIssueResolver"
RUN dotnet build "AutoIssueResolver.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AutoIssueResolver.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutoIssueResolver.dll"]
