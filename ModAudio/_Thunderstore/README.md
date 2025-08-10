# ModAudio

Replace game audio with your own mixtape.

You can use this mod to make audio pack mods, or to just replace audio on a whim.

Currently, `.ogg`, `.mp3` and `.wav` formats are supported, although `.ogg` and `.mp3` are preferred due to reduced file size.

# How to use

ModAudio loads custom audio and routing information from any custom mods you have in the plugins folder (`BepInEx/plugins/YourTeam-YourModName/audio`).

It uses a combination of audio files and the `__routes.txt` configuration file located in the audio folder of a mod.

It also loads custom audio from its own config folder (`BepInEx/config/Marioalexsan.ModAudio_UserAudioPack/audio`). 
You can place your custom audio in here if you don't want to make a standalone mod. The folder is normally created when you load the mod for the first time.

# Where is this audio folder located???

When using r2modman, you can find BepInEx in your profile folder for ATLYSS ("Settings" button, "Browse profile folder"). For example: `C:\Users\USERNAME\AppData\Roaming\r2modmanPlus-local\ATLYSS\profiles\Default`.

If you've installed BepInEx manually, then go to Steam's ATLYSS install path. For example: `C:\Program Files (x86)\Steam\steamapps\common\ATLYSS`.

From the r2modman profile or the Steam install, navigate to your downloaded audio pack's `BepInEx/plugins/AUDIOPACKNAME/audio` folder.

For custom user packs, you can use the `BepInEx/config/Marioalexsan.ModAudio_UserAudioPack/audio` folder.

# Audio mod example

Note: There are already plenty of audio mods created that use ModAudio on Thunderstore. If something in here is confusing, you can do "Manual download" for those audio mods to take a look at how they package their assets.

You can also use the following templates as examples:
- [Template Mod by RockOn](https://thunderstore.io/c/atlyss/p/RockOn/MusicModTemplate/) on Thunderstore, that shows you how to use the basic route format
- [ModAudioTemplate](https://github.com/Marioalexsan/AtlyssMods/tree/main/ModAudioTemplate) on GitHub, that shows you the full format available

## Basic audio pack format (v1.1.0)

Here's an example of a simple audio pack that replaces in-game audio using the basic format:

**mod folder structure under BepInEx/plugins**
```
ModFolder
|- audio
   |- __routes.txt
   |- _mu_flyby.mp3
   |- darkest_dungeon_combat.mp3
   |- risk_of_rain_2_boss.wav
   |- team_fortress_2_loadout.ogg
   |- cod_hitsound_01.mp3
   |- cod_hitsound_02.mp3
   |- cod_hitsound_03.mp3
```

**__routes.txt file contents**
```
# Empty lines and lines that start with a hashtag are ignored

# Musicss
_mu_wonton5 = darkest_dungeon_combat
_mu_ekca = risk_of_rain_2_boss
_mu_selee = team_fortress_2_loadout

# Hit sounds
weaponHit_Normal(average) = cod_hitsound_01
weaponHit_Normal(average) = cod_hitsound_02 / 0.5
weaponHit_Normal(average) = cod_hitsound_03 / 0.5
weaponHit_Normal(average) = ___default___ / 2
```

This audio mod will do the following:
- play `_mu_flyby.mp3` instead of `_mu_flyby` (main menu music) - implicitly because the file name matches the clip
- play `darkest_dungeon_combat.mp3` instead of `_mu_wonton5` (Grove combat)
- play `risk_of_rain_2_boss.wav` instead of `_mu_ekca` (Grove boss music)
- play `team_fortress_2_loadout.ogg` instead of `_mu_selee` (Character selection music)
- play one of `cod_hitsound_01.mp3`, `cod_hitsound_02.mp3` or `cod_hitsound_03.mp3` for Normal type average weapon hits
  - the total weight for this is `1 (implicit) + 0.5 + 0.5 + 2 = 4`
  - `cod_hitsound_01.mp3` will play with a `1 / 4 * 100% = 25%` chance
  - `cod_hitsound_02.mp3` will play with a `0.5 / 5 * 100% = 12.5%` chance
  - `cod_hitsound_03.mp3` will play with a `0.5 / 5 * 100% = 12.5%` chance
  - the original, unmodified sound clip will play with a `2 / 4 * 100% = 50%` chance

## Advanced audio pack format (v2.0.0)

The new format for __routes.txt in 2.0.0 uses a different syntax that gives you access to new features, such as overlays, effects, and other stuff.

You can check out the [ModAudioTemplate](https://github.com/Marioalexsan/AtlyssMods/tree/main/ModAudioTemplate) example audio pack for reference.

The format is documented in greater detail within the [__routes.txt](https://github.com/Marioalexsan/AtlyssMods/blob/main/ModAudioTemplate/audio/__routes.txt) file from it.

# Packaging your audio mods for Thunderstore / r2modman

When packaging your mods for Thunderstore / r2modman, you need to put your `audio` folder that contains your audio and `__routes.txt` under the `plugins` folder in the zip.

This is to make sure that r2modman won't flatten all of your files into the root directory, which ***will*** cause issues.

Here's an example of how your ZIP package should look like:

***yourmod.zip***
```
|- manifest.json
|- README.md
|- CHANGELOG.md
|- icon.png
|- plugins
   |- audio
      |- __routes.txt
      |- _mu_flyby.mp3
      |- someaudio.ogg
      |- someotheraudio.wav
```

r2modman will take all of your content from the ZIP's `plugins` and put it as-is in the mod folder, thus preserving the folder structure that ModAudio wants.

Also, your manifest.json should have ModAudio listed as a dependency, with the latest version being preferable:

***manifest.json***
```
{
  "name": "YourModName",
  "description": "Cool sounds and stuff",
  "version_number": "1.0.0",
  "dependencies": [
    "BepInEx-BepInExPack-5.4.2100",
    "Marioalexsan-ModAudio-2.0.0"
  ],
  "website_url": "https://github.com/Marioalexsan/AtlyssMods"
}
```

*Do not include any of the DLLs from ModAudio in your own mod.* It's not needed, and it might cause issues with loading. You just need the dependency string in manifest.json.

Additionally, it would be nice if you would make a cool `icon.png` for your mod and a `README.md` file with details about your audio pack.

You should also add some contact information of some kind in the `README.md` for people to send in feedback or bug reports.

# Additional details about the basic format (1.1.0)

## Direct replacement

You can replace audio clips directly if the file name matches the clip name within the game.

1. Choose a clip you want to replace (for example, `_mu_wonton5` - Crescent Grove's action music).
2. Take your custom audio, and rename it so that it has the same name as the clip (for example, `coolmusic.mp3` -> `_mu_wonton5.mp3`).
3. Place the audio file in the `audio` folder.

Think of it as an implicit `sourceClip = sourceClip` route.

## Reroute the clip in __routes.txt

You can do custom replacements by specifying replacement information in the `__routes.txt` file.

1. Choose a clip you want to replace (for example, `_mu_wonton5` - Crescent Grove's action music).
2. Take your custom audio, and put it in the `audio` folder (for example, `coolmusic.mp3`). The audio file can have any name you want.
3. Create a `__routes.txt` file in the `audio` folder, or open it with Notepad if it already exists.
4. Add the clip name and the file name without the extension, separeted by `=` on a new line : `_mu_wonton5 = coolmusic`.

This will tell ModAudio to play `coolmusic.mp3` every time `_mu_wonton5` would play in the game.

If you add multiple lines that have use the same clip, then ModAudio will play one of them randomly.

For example, `firstbossmusic.mp3` and `secondbossmusic.mp3` will each play about half of the time:

```
_mu_wonton5 = firstbossmusic
_mu_wonton5 = secondbossmusic
```

If you want clips to play with different chances, you can specify a number as a weight for the random roll. Separate it from the audio file name with a `/`.

For example, `firstbossmusic` will play 2/3rds of the time (1.0 / (1.0 + 0.5)), and `secondbossmusic` will play 1/3rd of the time (0.5 / (1.0 + 0.5)):

```
_mu_wonton5 = firstbossmusic / 1.0
_mu_wonton5 = secondbossmusic / 0.5
```

If you don't specify a weight, then it defaults to 1.

Finally, if you want to tell ModAudio to play the original clip, you can use the special `___default___` identifier instead of a file name:

```
_mu_wonton5 = ___default___ / 0.8
_mu_wonton5 = _mu_wonton5_remix / 0.2
```

This will play the default boss music with an 80% chance, and a remixed version with a 20% chance.

## This __routes.txt format sucks! Can I just use JSON or something?

Yeah! You can specify a `modaudio.config.json` file instead of a `__routes.txt` to customize the audio packs using JSON.

The feature set available for JSON configs is slightly bigger, and you can use [this file](https://github.com/Marioalexsan/AtlyssMods/blob/main/ModAudio/AudioPackConfig.cs) as a reference for all of the fields that are available.

## Multiple audio mods

If you use multiple audio mods that replace the same audio clips, ModAudio will effectively combine them into one.

For example, if you have two mods that replace the Main Menu music, then each of them will have a 50% chance to play.

If the first mod's replacement has a weight of 1, and the second one has a weight of two, then it will be a 33% / 67% chance split for either of them to play.

For mods with overlays, each of the mods will trigger overlays independently, so you might want to not install too many hitsound audio packs at once.

## Audio logging

There are multiple options available for logging how audio packs are loaded and played. All of the options can be configured in `BepInEx/config/Marioalexsan.ModAudio.cfg` (generated on first load), or in Nessie's EasySettings.

- LogPackLoading - logs details about loaded packs - default: true
- LogAudioPlayed - logs details about audio that is played in-game - default: true
- UseMaxDistanceForLogging - if true, only audio that is within a certain distance from the player will be logged - default: false
- MaxDistanceForLogging - distance from player to log audio within, in units. For reference, Angela is about 12 units or so in height - default: 32, min: 32, max: 2048

For each audio pack loaded, there is also an option to enable or disable that pack.

# Mod Compatibility

ModAudio targets the following game versions and mods:
- ATLYSS v1.6.2b
- Nessie's EasySettings v1.1.3 (optional dependency used for configuration)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.
