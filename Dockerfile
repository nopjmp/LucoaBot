FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

# TODO: make this work for faster builds..
# COPY *.csproj ./
# RUN dotnet restore

COPY . ./
RUN dotnet publish -r linux-x64 -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "LucoaBot.dll"]