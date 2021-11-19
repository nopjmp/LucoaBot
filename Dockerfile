FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# TODO: make this work for faster builds..
# COPY *.csproj ./
# RUN dotnet restore

COPY . ./
RUN dotnet publish -r linux-x64 -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "LucoaBot.dll"]