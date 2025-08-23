# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2025-Aug-16 - "The Bugfest Development Hell Update"

```
Trust me when I say that these changelogs won't make too much sense.
If you want to find out how to use some of the new features, you'll either have to look through the mod's code,
or look at examples from upcoming audio packs to understand how to use the new features.

These features will be documented properly at a later point in time.
```

### Deprecated

- Removed unused and undocumented route options: `filter_by_sources`, `filter_by_object`
  - These features can instead be implemented via the scripting engine
- Removed a feature that would automatically replace clips based on whenever the audio files match the name of the vanilla clips 
  - If you have an audio pack that used this feature, you now have to explicitly do the replacement in the audio pack configuration
- Removed option to specify routes via JSON configuration in `modaudio.config.json`
  - This might be replaced by JS script configurations when full scripting support happens

### Added

- **IMPORTANT**: ModAudio now has limited experimental support for scripting via JavaScript!
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
- ModAudio now allows forcing action music to play as part of maps by using scripts

### Changed

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