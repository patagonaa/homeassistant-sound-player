# homeassistant-sound-player
Play Random Sounds via HomeAssistant.
Multiple sound pools can be configured, each one will add a new switch to Home Assistant.

See `"src/HomeAssistantUnifiLed/configs/appSettings.example.json"` for a config example.

## Dependencies
`ffplay` has to be installed for audio playback. In the Dockerfile, this will be installed at build time automatically.

If you don't want to use this via Docker (or on Windows), make sure ffplay is available in `PATH`.

## Installation
Use the docker-compose file or use some other way to do `dotnet HomeAssistantSoundPlayer.dll` and keep the app running.
