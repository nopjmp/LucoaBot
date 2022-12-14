FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /source

# COPY *.csproj .
# RUN dotnet restore

COPY . .
RUN dotnet publish -c release -o /app -r linux-x64 --self-contained false

FROM mcr.microsoft.com/dotnet/runtime:7.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "LucoaBot.dll"]