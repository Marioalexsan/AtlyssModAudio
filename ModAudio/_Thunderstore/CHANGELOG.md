# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.3.0] - 2025-Sep-17

### Deprecated
- JS scripting is deprecated, and is planned to be removed in the next major release
  - The reason is severe performance and memory usage issues from Jint even for simple scripts
  - This will likely be replaced with another scripting tech (possibly MoonSharp / Lua) as soon as I figure out a good alternative

### Changed
- Added a detection rate setting for playOnAwake sources that allows adjusting its performance impact
  - It has four options: Realtime (instant), Fast (100ms), Medium (500ms) and Slow(2500ms)
  - The current default option is Fast (100ms); versions prior to this acted as if they used Realtime (instant)
  - Slower options reduce CPU load, but may fail to properly replace / overlay playOnAwake sources

### Fixed
- One shot sources now correctly stop when AudioSource.Stop() is called on the original audio source (either by the game or other mods)
- Routing now correctly skips over routes that are already present in a chain
- Fix some performance issues with non-script routing code

## [3.2.0] - 2025-Aug-30

### Added

- Added a `force_play` effect: setting `~ force_play : true` will cause audio to be forced to play as part of dynamic targeting, even if the audio source is stopped at the moment
  - This can be useful for enforcing that the switched music will play when the group is changed; for example, going from a stopped day music for Outer Sanctum, to a playing day music for Chapel of the Elders's subregion

## [3.1.2] - 2025-Aug-30

### Fixed

- Fixed `force_loop` not working as expected with multiple routes
- Fixed `force_loop` still using `false` as a default value, causing it to force looping off by default instead of using existing loop settings

## [3.1.1] - 2025-Aug-30

### Fixed

- Fixed a skill issue from the mod author where the changelog and readme for 3.1.0 was not updated

## [3.1.0] - 2025-Aug-30

### Changed

- `~ force_loop : false` will now force audio sources to **not** loop if it's specified; previously, it was a no-op as far as audio sources were concerned
  - `~ force_loop : true` will still force looping on, and not specifying the property will make the audio source use whatever looping properly it normally uses

### Fixed

- Fixed audio packs displaying their name as "<null>" when calling `console.log()` in the global scope of the JavaScript module
- Fixed route numbers (volume, pitch, weights, etc.) incorrectly using commas on some PCs with cultures / languages that normally use commas for decimal separators

## [3.0.2] - 2025-Aug-26

### Fixed

- Fixed overlays not playing at all.
- Fixed aggroed creeps not being tracked correctly for scripts (mostly relevant for Combat Audio Pack).
- Fixed engine reloads causing some one shot sources to continuously "lose" volume and get silenced with multiple reloads. This includeed changing audio pack active state in EasySettings.
- Tiny optimizations on some methods, nothing too significant though.

## [3.0.1] - 2025-Aug-25

### Changed

- Added a WIP tab to the debug menu that shows current count of audio sources in the level, and count of audio sources tracked by ModAudio

### Fixed

- Fixed an issue where one shot audio sources wouldn't get cleaned up properly, causing increasingly worse performance while staying in a level

## [3.0.0] - 2025-Aug-24 - "The Bugfest Development Hell Update"

```
Trust me when I say that these changelogs won't make too much sense, or that they might be incomplete.
If you want to find out how to use some of the new features, you'll either have to 
look through the mod's code, or look at examples from upcoming audio packs to understand them.

These features will be documented properly at a later point in time.
```

### Removed

- Removed unused and undocumented route options: `filter_by_sources`, `filter_by_object`
  - These features can instead be implemented via the JS scripting engine
- Removed automatic replacement of clips based on audio files matching vanilla files
  - If you have an audio pack that used this, you now have to explicitly do the replacement in the audio pack config
- Removed option to specify routes via JSON configuration in `modaudio.config.json`, since it was largely unused
  - This might be replaced by JS script configurations when full scripting support happens

### Added

- **IMPORTANT**: ModAudio now has experimental support for scripting via JavaScript!
  - Scripts are specified with a `__routes.js` file alongside `__routes.txt`
  - Added the `target_group_script` parameter for routes, which specifies a script function that will select a group of target clips to play based on the conditions you specify
  - Added the `enable_dynamic_targeting` parameter for routes, which specifies that audio will be updated dynamically using `target_group_script`; this allows for dynamic map music or conditional routing
- Added a route effect to force audio sources to loop even if the original tracks didn't loop
  - usable using the `force_loop` effect: `source = replacement ~ force_loop : true`
- Added multiple previously unavailable route effects. Their effects are documented in `AudioPackConfig.cs`. The full list of effects is now as follows:
  - `link_overlay_and_replacement` - true/false
  - `relative_replacement_effects` - true/false
  - `overlay_stops_if_source_stops` - true/false
  - `relative_overlay_effects` - true/false
  - `overlays_ignore_restarts` - true/false
  - `target_group_script` - string
  - `enable_dynamic_targeting` - true/false
  - `replacement_weight` (alternatively `weight`) - number
  - `volume` - number
  - `pitch` - number
  - `force_loop` - true/false
- Added a global option for audio packs to specify a script function to be called every frame via `%updatescript`
  - Example: `%updatescript pack_update`
  - This allows you to check game state and track various conditions, or call engine methods
- Added a fifth parameter for replacement clips - this represents a group that can be selected by `target_group_script`
  - New syntax: `clipname : weight : volume : pitch : group` 
  - This does not apply to overlays; they cannot receive groups
- Comments can now be used anywhere, and will apply until the end of the line, and you can now also format your routes across multiple lines by using a terminating `\` character
  - Example:
    ```
    # Combat music
    modaudio_map_crescentroad_action = <atlyss>_mu_wonton5 # Everything after hashtags is ignoerd

    # My cool route
    _mu_haven                                             \ # This is a comment, we're using \ to separate the route across multiple lines
      = | ___default___ : 1.0 : 1.0 : 1.0 : nonarena      \ # This is another comment
        | <atlyss>_mu_hell02 : 1.0 : 1.0 : 1.0 : arena    \
      ~ | target_group_script : target_group_sanctumarena \
        | enable_dynamic_targeting : true                 \
        | smooth_dynamic_targeting : true                   # This does not end with a \ character since it's the end of the route
    ```
- Within lists in `__routes.txt` (marked by the `|` character), you can now use a leading or trailing pipe character for formatting purposes without it causing issues
- ModAudio now allows you to use vanilla clips as part of replacements and overlays. You can specify a vanilla clip by prepending it with `<atlyss>`
  - For example: `_mu_haven = <atlyss>_mu_wonton5`
- ModAudio now tries to add placeholder empty audio sources for maps that do not have them, which should allow defining custom music for them.
  - The exact custom source names that you need to use in `__routes.txt` will be logged in the console 
  - For example:
    - day music: `modaudio_map_{clean map name}_day`
    - night music: `modaudio_map_{clean map name}_night`
    - action music: `modaudio_map_{clean map name}_action`
    - examples: `modaudio_map_tuulvalley_action`, `modaudio_map_executionerstomb_action`, `modaudio_map_crescentroad_action`
- ModAudio now allows forcing action music to play as part of maps, via JS scripts
- Added chain routing functionality - by using `~ chain_route : true`, you can tell ModAudio that the replacement of your route should be rerouted again through other packs
  - This can be useful when you reroute audio to vanilla clips, and want those vanilla clips to actually play whichever custom clips the user has from other packs
  - There is a limit of 4 max chained routes

### Changed

- ***IMPORTANT***: A new debug menu has been implemented, which can be opened by using the `DebugMenuButton` key configuration (see EasySettings or the config file)
  - All audio logging functionality has been moved to the debug menu
  - As a side effect, this means that the BepInEx console will no longer be cluttered by ModAudio logs
  - The debug menu also includes additional debug information about currently loaded audio packs
  - Any audio packs that have encountered errors will be displayed in red - check the error / warning messages in the audio logs to understand what went wrong
  - The distance filter has been removed temporarily - this will be reimplemented in a later update
- ModAudio now tries to implement / fix "null" audio sources, so that they play correctly
  - You might notice that Sanctum Arena and Executioner's Tomb now play background music, unlike before
- Changed vanilla audio mixing logic for map instances so that the transition from action music to day / night music is smoother 

### Fixed

- Fixed hard boss music not playing / routing properly (notably Valdur in Crescent Grove)
- Fixed an issue with custom clip volumes not applying to in-memory audio files

## [2.2.2] - 2025-May-22

### Fixed

- Fixed replacement weight not working as expected with `source = replacement / weight` routes
- Switched from UnityWebRequestMultimedia to NAudio for loading audio files (previously was only used for streaming)
  - This should fix an issue where some MP3 files wouldn't load / play correctly, but audio might load slower as a result
- Fixed mod crashing if an exception is thrown while reading route files
- Fixed warning log spam when EasySettings version would differ from the expected version (should now warn at most once)

### Changed

- Audio packs should now be sorted by display name in EasySettings

## [2.2.1] - 2025-Mar-31

### Fixed

- Fixed an error that would occur rarely when sounds are stopped
- (Hopefully) Fixed an error that would occur rarely when reloading audio

## [2.2.0] - 2025-Mar-30

### Added

- Added a button in EasySettings that opens the custom / user audio pack folder for easy access

### Fixed

- Fixed some overlay sounds being interrupted mid-playthrough when played from components that have particle system components
  - Technial details: The sound is played from a game object higher in the hierarchy as a workaround for the game object being disabled by the particle system
- Fixed routing failing occasionally when using `___nothing___` as a replacement
- Fixed game object names being erroneously set to "oneshot" for display purposes
  - It will now use the correct object name and instead append "oneshot" to logged lines for played audio

## [2.1.0] - 2025-Mar-29

### Added

- Added an effect option "overlays_ignore_restarts" for `__routes.txt` routes (`originalClip @ overlayClip ~ overlays_ignore_restarts : true`)
  - Settings this as true will make it so that an overlay sound is played only once if an audio source is restarted multiple times.

### Changed

- Slightly improved load times by skipping loading audio data for audio packs that are disabled
- Mod assembly now uses an embedded PDB instead of a separate PDB file for debugging

### Fixed

- Fixed some playOnAwake sources being logged an extra time in the console
- Fixed pitch and volume being tracked and applied incorrectly for cast effect sounds

## [2.0.1] - 2025-Mar-19

### Changed

- LogAudioPlayed is now false by default
- The PDB is now supplied alongside the mod for debug purposes

### Fixed

- Fixed crash caused by audio sources with no output audio mixer group
- Added a bit of error handling

## [2.0.0] - 2025-Mar-16

### Added

- Added volume and pitch controls for tracks
- Added "overlay" sounds that play alongside a track instead of replacing it completely
- Rewrote the __routes.txt pack format to support the changes from above
- Added a modaudio.config.json pack format that allows full control over pack loading
- Improved load performance by streaming audio for large clips (over 1MB) that use .ogg, .mp3 or .wav
- Added support for Nessie's EasySettings mod
- Added support for reloading audio packs when using EasySettings, allowing you to add, remove and edit packs while you're playing
- Added support for toggling audio packs on and off when using EasySettings
- Added options for toggling console logging on and off when using EasySettings
- Improved audio logging, and added a configuration setting to filter out audio that plays too far from the player (when the main player is available)
- Added options to filter logged audio based on its audio group

### Changed

- Audio logging now includes way more information in a concise manner

### Fixed

- Fixed post-boss defeat music not being replaceable 
- Fixed functionality being broken for audio sources that utilized `playOnAwake` for playing

### Removed

- OverrideCustomAudio option has been removed. It may or may not return in the future, but you can always manually edit the packs you download in case you need adjustments

## [1.1.0] - 2025-Jan-01

### Added

- You can now specify multiple audio files per source clip in __routes.txt. ModAudio will play one at random, based on their weights.
- Each clip in __routes.txt can now specify a weight using the format `source = replacement / weight` (where weight is a decimal number, i.e. `1.0`). This is used for random rolls when there are multiple clips present.
- You can now use the `___default___` identifier when replacing audio. This allows you to include the vanilla audio as a clip that should be played randomly.
- Added an `OverrideCustomAudio` option that can be used to override any custom audio from mods with whatever you specify in ModAudio itself.
- `__routes.txt` now supports line comments. Lines starting with `#` will be treated as a comment and ignored.

## [1.0.4] - 2024-Dec-28

### Changed

- Improved audio loading from root folder, it will now also load audio from root if there's at least one clip with a known vanilla name

## [1.0.3] - 2024-Dec-27

### Changed

- The mod will now load assets from mods that don't have a DLL plugin. It will load any audio from the "audio" folder (i.e. `BepInEx/plugins/Your-Mod/audio`) if it exists. It will also load audio from the root folder (`BepInEx/plugins/Your-Mod/`) if there is a `__routes.txt` file present

### Fixed

- Fixed an issue related to using the mod without any audio under `ModAudio/audio`

## [1.0.2] - 2024-Dec-23

### Changed

- Removed NAudio dependency in favor of UnityWebRequestMultimedia

### Fixed

- Audio played via `AudioSource.PlayOneShot` should now be replaced correctly

## [1.0.1] - 2024-Dec-23

### Added

- Option to enable verbose logging of audio sources that are being replaced, and audio sources that are loaded in

### Changed

- Improve reliability of audio replacement when scenes are loaded

## [1.0.0] - 2024-Dec-22

### Changed

**Initial mod release**