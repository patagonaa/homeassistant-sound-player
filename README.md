# homeassistant-sound-player
Play sounds via MQTT (with Home Assistant autodiscovery).
Multiple sound pools can be configured, each one will add a new switch to Home Assistant.

Sounds can either be played randomly (default) or with a custom `ISoundSequenceProvider`.
An example SoundSequenceProvider to tell the current time including the required sounds in german by Zabex is provided (see his microcontroller project to tell the time at https://www.zabex.de/frames/169zeitansage.html).

See `"src/HomeAssistantUnifiLed/configs/appSettings.example.json"` for a config example.

## Dependencies
`ffmpeg` and `ffplay` has to be installed for audio playback. In the Dockerfile, this will be installed at build time automatically.

If you don't want to use this via Docker (or on Windows), make sure ffplay is available in `PATH`.

## Configuration
By default, the app looks for an `configs/appSettings.json` file or environment variables. An example config is provided in `src/HomeAssistantSoundPlayer/configs/appSettings.example.json`.

## Build
Make sure all submodules are checked out by either cloning this via `git clone --recurse-submodules` or running `git submodule update --init`.
Afterwards, a simple `dotnet run --project src/HomeAssistantSoundPlayer/HomeAssistantSoundPlayer.csproj` or Visual Studio build should be enough to get it up and running.

## Installation
Use the docker-compose file or use some other way to run `dotnet HomeAssistantSoundPlayer.dll` and keep it running.
