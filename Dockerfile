FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app/src
# copy csproj only so restored project will be cached
COPY src/HomeAssistantSoundPlayer/HomeAssistantSoundPlayer.csproj /app/src/HomeAssistantSoundPlayer/
RUN dotnet restore HomeAssistantSoundPlayer/HomeAssistantSoundPlayer.csproj
COPY src/ /app/src
RUN dotnet publish -c Release HomeAssistantSoundPlayer/HomeAssistantSoundPlayer.csproj -o /app/build

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
RUN apt update && apt install -y ffmpeg alsa-utils
WORKDIR /app
COPY --from=build /app/build/ ./
ENTRYPOINT ["dotnet", "HomeAssistantSoundPlayer.dll"]