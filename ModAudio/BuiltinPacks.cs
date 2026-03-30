using BepInEx.Logging;

namespace Marioalexsan.ModAudio;

static class BuiltinPacks
{
    internal static void LoadBuiltinAudioPacks(List<AudioPack> existingPacks)
    {
        LoadKnuckles(existingPacks);
    }
    
    private static void LoadKnuckles(List<AudioPack> existingPacks)
    {
        if (!ModAudio.EasterEggsEnabled.Value)
            return;
        
        try
        {
            AudioPack knucklesPack = new AudioPack()
            {
                Flags = PackFlags.NotConfigurable | PackFlags.BuiltinPack,
                Config =
                {
                    DisplayName = "ModAudio Builtin",
                    Id = "ModAudio_Knuckles",
                    Routes =
                    [
                        new Route()
                        {
                            OriginalClips = ["_mu_flyby"],
                            ReplacementClips =
                            [
                                new ClipSelection()
                                {
                                    Name = "knuckles"
                                }
                            ],
                            // Virtually guaranteed chance since I don't want to implement audio pack priorities
                            ReplacementWeight = 1e20f,
                        }
                    ],
                    CustomClips =
                    [
                        new AudioClipData()
                        {
                            Name = "knuckles",
                            Path = Path.Combine(ModAudio.AssetsFolder, "knuckles.ogg")
                        }
                    ]
                }
            };

            existingPacks.Add(knucklesPack);

            if (ModAudio.Knuckles)
                knucklesPack.SetFlag(PackFlags.Enabled);
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Warning, "Couldn't load the Knuckles easter egg! Please report this to the mod developer.");
            AudioDebugDisplay.LogEngine(LogLevel.Warning, $"Exception data: {e}");
        }
    }
}