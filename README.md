# tidewatch
Tidewatch: a pirate-era island tower defense game (Unity WebGL)

## Build (Unity 6000.3.26f1, WebGL)
`Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log` (no `-quit`;
the build validates `Assets/Resources/Config/tidewatch.json`, writes the release to `/docs`, and exits). `TW_DEV=1` = dev build in `Builds/WebGLDev`.
On the shared box: run `pgrep -x Unity` first and wrap the launch in `flock /tmp/unity-build.lock`.

Waves, enemies, defenses, lanes and slots live in `Assets/Resources/Config/tidewatch.json`. Art and audio: see `Assets/ART_AUDIO_LIST.md`.
Controls: tap/click a plot or buoy to build, upgrade or sell; Space = start/call wave, F = 1x/2x, P/Esc = pause, R = restart.
