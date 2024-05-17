FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# COPY *.csproj .
# RUN dotnet restore

COPY . .
RUN dotnet publish --configuration Release --runtime linux-musl-x64 --self-contained false -o /app 

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "LucoaBot.dll"]