# homeassistant-sound-player
Play sounds via MQTT (with Home Assistant autodiscovery).
Multiple sound pools can be configured, each one will add a new switch to Home Assistant.

Sounds can either be played randomly (default) or with a custom `ISoundSequenceProvider`.
An example SoundSequenceProvider to tell the current time including the required sounds in german by Zabex (see his microcontroller project to tell the time at <http://www.zabex.de/site/169zeitansage.html>) is provided.

See `"src/HomeAssistantUnifiLed/configs/appSettings.example.json"` for a config example.

## Dependencies
`ffplay` has to be installed for audio playback. In the Dockerfile, this will be installed at build time automatically.

If you don't want to use this via Docker (or on Windows), make sure ffplay is available in `PATH`.

## Installation
Use the docker-compose file or use some other way to run `dotnet HomeAssistantSoundPlayer.dll` and keep it running.
