FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine3.17 AS build
WORKDIR /source

# COPY *.csproj .
# RUN dotnet restore

COPY . .
RUN dotnet publish --configuration Release --runtime linux-musl-x64 --self-contained false -o /app 

FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine3.17
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "LucoaBot.dll"]