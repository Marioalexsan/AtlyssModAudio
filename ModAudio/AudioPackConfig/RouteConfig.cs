using System.Globalization;

namespace Marioalexsan.ModAudio;

public class RouteConfig
{
    private static readonly char[] ReplacementSeparator = ['='];
    private static readonly char[] OverlaySeparator = ['@'];
    private static readonly char[] EffectSeparator = ['~'];
    private static readonly char[] RouteSeparators = [.. ReplacementSeparator, .. OverlaySeparator, .. EffectSeparator];

    private static readonly char[] FieldSeparator = [':'];
    private static readonly char[] ListSeparator = ['|'];

    public Dictionary<string, float> ClipVolumes { get; set; } = [];
    public Dictionary<string, string> ClipPaths { get; set; } = [];
    public List<Route> Routes { get; set; } = [];
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string UpdateScript { get; set; } = "";
    public bool EnabledByDefault { get; set; } = true;

    // Note: this format is stupid and dumb and I hate it and ugh why
    public static void ReadTextFormat(Stream stream, RouteConfig pack, Action<int, string> errorLogger)
    {
        using var streamReader = new StreamReader(stream);

        int lineNumber = 0;

        while (!streamReader.EndOfStream)
        {
            string routeText = "";

            int routeStartLine = lineNumber + 1;

            bool badLineContinuation = false;

            // Check whenever it's a multiline route, i.e. uses backspaces to continue a routeText:
            // source \
            // = replacement \
            // @ overlay

            string? nextLine;
            while ((nextLine = streamReader.ReadLine()) != null)
            {
                lineNumber++;

                // Take FIRST hashtag, since a comment starts at the first character, not the last one
                var commentPos = nextLine.IndexOf('#');

                // Remove comments
                if (commentPos != -1)
                    nextLine = nextLine[..commentPos];

                var backslashPos = nextLine.LastIndexOf('\\');

                if (backslashPos == -1)
                {
                    routeText += nextLine;
                    break;
                }

                for (int i = backslashPos + 1; i < nextLine.Length; i++)
                {
                    if (!char.IsWhiteSpace(nextLine[i]))
                    {
                        badLineContinuation = true;
                        break;
                    }
                }

                // Remove backslash and whitespace
                nextLine = nextLine[..backslashPos];
                routeText += nextLine;
            }

            if (badLineContinuation)
            {
                errorLogger(lineNumber, $"Expected only whitespace and comments after line continuation (\"\\\")");
                continue;
            }

            if (routeText.Trim() == "")
                continue; // Empty routeText

            if (routeText.Trim().StartsWith("%"))
            {
                ParseGlobalParamer(routeText, errorLogger);
                continue;
            }

            if (routeText.Contains("/") || routeText.IndexOfAny(EffectSeparator) == -1 && routeText.IndexOfAny(OverlaySeparator) == -1 && routeText.IndexOfAny(FieldSeparator) == -1 && routeText.IndexOfAny(ListSeparator) == -1)
            {
                ParseSimpleRouteFormat(routeText, errorLogger);
                continue;
            }

            if (!SplitRouteParts(routeText, out var clipNames, out var replacements, out var overlays, out var effects, errorLogger))
                continue;

            var route = new Route
            {
                OriginalClips = clipNames.Where(x => !x.StartsWith(ModAudioGame.AliasNameStart)).ToList(),
                OriginalClipAliases = clipNames.Where(x => x.StartsWith(ModAudioGame.AliasNameStart)).Select(x => x.Substring(ModAudioGame.AliasNameStart.Length)).ToList(),
            };

            for (int i = 0; i < replacements.Count; i++)
            {
                var fields = replacements[i].Split(FieldSeparator).Select(x => x.Trim()).ToArray();

                if (fields.Length > 5)
                {
                    errorLogger(routeStartLine, $"Too many values defined for a target clip (expected at most 5), skipping it.");
                    continue;
                }

                var replacementName = fields[0];

                if (replacementName == "")
                {
                    errorLogger(routeStartLine, $"Empty clip, ignoring it.");
                    replacements.RemoveAt(i--);
                }

                var randomWeight = ModAudio.DefaultWeight;
                var volume = 1f;
                var pitch = 1f;
                var group = "";

                if (fields.Length > 1 && !float.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out randomWeight))
                    errorLogger(routeStartLine, $"Couldn't parse random weight {fields[1]} for {replacementName}, defaulting to {randomWeight}.");

                if (fields.Length > 2 && !float.TryParse(fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out volume))
                    errorLogger(routeStartLine, $"Couldn't parse volume {fields[2]} for {replacementName}, defaulting to {volume}.");

                if (fields.Length > 3 && !float.TryParse(fields[3], NumberStyles.Number, CultureInfo.InvariantCulture, out pitch))
                    errorLogger(routeStartLine, $"Couldn't parse pitch {fields[3]} for {replacementName}, defaulting to {pitch}.");

                if (fields.Length > 4)
                    group = fields[4];

                route.ReplacementClips.Add(new()
                {
                    Name = replacementName,
                    Weight = randomWeight,
                    Volume = volume,
                    Pitch = pitch,
                    Group = group
                });
            }

            pack.Routes.Add(route);

            for (int i = 0; i < overlays.Count; i++)
            {
                var fields = overlays[i].Split(FieldSeparator).Select(x => x.Trim()).ToArray();

                if (fields.Length > 4)
                {
                    errorLogger(routeStartLine, $"Too many values defined for a target clip (expected at most 4), skipping it.");
                    continue;
                }

                var overlayName = fields[0];

                if (overlayName == "")
                {
                    errorLogger(routeStartLine, $"Empty clip, ignoring it.");
                    overlays.RemoveAt(i--);
                }

                var randomWeight = ModAudio.DefaultWeight;
                var volume = 1f;
                var pitch = 1f;

                if (fields.Length > 1 && !float.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out randomWeight))
                {
                    errorLogger(routeStartLine, $"Couldn't parse random weight {fields[1]} for {overlayName}, defaulting to {ModAudio.DefaultWeight}.");
                }

                if (fields.Length > 2 && !float.TryParse(fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out volume))
                {
                    errorLogger(routeStartLine, $"Couldn't parse volume {fields[2]} for {overlayName}, defaulting to 1.");
                }

                if (fields.Length > 3 && !float.TryParse(fields[3], NumberStyles.Number, CultureInfo.InvariantCulture, out pitch))
                {
                    errorLogger(routeStartLine, $"Couldn't parse pitch {fields[3]} for {overlayName}, defaulting to 1.");
                }

                route.OverlayClips.Add(new()
                {
                    Name = overlayName,
                    Weight = randomWeight,
                    Volume = volume,
                    Pitch = pitch
                });
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var fields = effects[i].Split(FieldSeparator).Select(x => x.Trim()).ToArray();

                if (fields.Length == 1)
                {
                    errorLogger(routeStartLine, $"Expected a value for {fields[0]}.");
                    continue;
                }

                bool parsedValue = false;

                switch (fields[0])
                {
                    case "link_ovl_repl":
                    case "link_overlay_and_replacement":
                        if (parsedValue = bool.TryParse(fields[1], out bool link_overlay_and_replacement))
                            route.LinkOverlayAndReplacement = link_overlay_and_replacement;
                        break;
                    case "rel_repl_fx":
                    case "relative_replacement_effects":
                        if (parsedValue = bool.TryParse(fields[1], out bool relative_replacement_effects))
                            route.RelativeReplacementEffects = relative_replacement_effects;
                        break;
                    case "ovl_stop_with_src":
                    case "overlay_stops_if_source_stops":
                        if (parsedValue = bool.TryParse(fields[1], out bool overlay_stops_if_source_stops))
                            route.OverlayStopsIfSourceStops = overlay_stops_if_source_stops;
                        break;
                    case "rel_ovl_fx":
                    case "relative_overlay_effects":
                        if (parsedValue = bool.TryParse(fields[1], out bool relative_overlay_effects))
                            route.RelativeOverlayEffects = relative_overlay_effects;
                        break;
                    case "ovl_ign_restart":
                    case "overlays_ignore_restarts":
                        if (parsedValue = bool.TryParse(fields[1], out bool overlaysIgnoreRestarts))
                            route.OverlaysIgnoreRestarts = overlaysIgnoreRestarts;
                        break;
                    case "tg_lua":
                    case "target_group_script":
                        route.TargetGroupScript = fields[1];
                        parsedValue = true;
                        break;
                    case "tg_dyn":
                    case "enable_dynamic_targeting":
                        if (parsedValue = bool.TryParse(fields[1], out bool enableDynamicTargeting))
                            route.EnableDynamicTargeting = enableDynamicTargeting;
                        break;
                    case "tg_smooth":
                    case "smooth_dynamic_targeting":
                        if (parsedValue = bool.TryParse(fields[1], out bool smoothDynamicTargeting))
                            route.SmoothDynamicTargeting = smoothDynamicTargeting;
                        break;
                    case "chain":
                    case "chain_route":
                        if (parsedValue = bool.TryParse(fields[1], out bool allowChainRouting))
                            route.UseChainRouting = allowChainRouting;
                        break;
                    case "w":
                    case "rw":
                    case "weight":
                    case "replacement_weight":
                        if (parsedValue = float.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float replacementWeight))
                            route.ReplacementWeight = replacementWeight;
                        break;
                    case "v":
                    case "vol":
                    case "volume":
                        if (parsedValue = float.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float volume))
                            route.Volume = volume;
                        break;
                    case "p":
                    case "pit":
                    case "pitch":
                        if (parsedValue = float.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float pitch))
                            route.Pitch = pitch;
                        break;
                    case "fl":
                    case "force_loop":
                        if (parsedValue = bool.TryParse(fields[1], out bool force_loop))
                            route.ForceLoop = force_loop;
                        break;
                    case "fp":
                    case "force_play":
                        if (parsedValue = bool.TryParse(fields[1], out bool force_play))
                            route.ForcePlay = force_play;
                        break;
                    case "map":
                    case "map_name":
                        if (fields.Length == 1)
                        {
                            errorLogger(routeStartLine, $"Expected at least a value for {fields[0]}.");
                        }
                        else
                        {
                            route.MapNameCondition = fields[1..].ToList();
                        }
                        parsedValue = true;
                        break;
                    default:
                        errorLogger(routeStartLine, $"Unrecognized route effect / setting {fields[0]}.");
                        parsedValue = true;
                        break;
                }

                if (!parsedValue)
                    errorLogger(routeStartLine, $"Couldn't parse {fields[0]}.");
            }
        }

        bool SplitRouteParts(string line, out List<string> clipNames, out List<string> replacements, out List<string> overlays, out List<string> effects, Action<int, string> errorLogger)
        {
            clipNames = [];
            replacements = [];
            overlays = [];
            effects = [];

            var parts = line.Split(RouteSeparators, 5, StringSplitOptions.None);

            var replacementArrayIndex = -1;
            var overlayArrayIndex = -1;
            var effectArrayIndex = -1;

            if (parts.Length > 5 || parts.Length == 1)
            {
                errorLogger(lineNumber, $"Encountered a malformed route (expected source = replacement @ overlay ~ modifier), skipping it.");
                return false;
            }

            int nextIndex = line.IndexOfAny(RouteSeparators, 0);
            int nextArrayIndex = 1;

            if (nextIndex == -1)
            {
                errorLogger(lineNumber, $"Encountered a malformed route (expected source = replacement @ overlay ~ effect), skipping it.");
                return false;
            }

            bool invalidRoute = false;

            while (nextIndex != -1)
            {
                if (line[nextIndex] == ReplacementSeparator[0])
                {
                    if (replacementArrayIndex != -1 || overlayArrayIndex != -1 || effectArrayIndex != -1)
                    {
                        invalidRoute = true;
                        break;
                    }

                    replacementArrayIndex = nextArrayIndex++;
                }
                else if (line[nextIndex] == OverlaySeparator[0])
                {
                    if (overlayArrayIndex != -1)
                    {
                        invalidRoute = true;
                        break;
                    }

                    overlayArrayIndex = nextArrayIndex++;
                }

                else if (line[nextIndex] == EffectSeparator[0])
                {
                    if (effectArrayIndex != -1)
                    {
                        invalidRoute = true;
                        break;
                    }

                    effectArrayIndex = nextArrayIndex++;
                }

                nextIndex = line.IndexOfAny(RouteSeparators, nextIndex + 1);
            }

            if (invalidRoute)
            {
                errorLogger(lineNumber, $"Encountered a malformed route (expected source = replacement @ overlay ~ effect), skipping it.");
                return false;
            }

            bool overlaysComeFirst = overlayArrayIndex < effectArrayIndex;

            // Ignore empty list entries, this allows for nicer syntax by starting the first field with a pipe
            List<string> ParseListEntries(string part) => [.. part.Trim().Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))];

            clipNames = ParseListEntries(parts[0]);

            for (int i = 0; i < clipNames.Count; i++)
            {
                if (clipNames[i] == "")
                {
                    errorLogger(lineNumber, $"empty source clip, ignoring it.");
                    clipNames.RemoveAt(i--);
                }
            }

            if (clipNames.Count == 0)
            {
                errorLogger(lineNumber, $"Expected at least one valid source clip, skipping route.");
                return false;
            }

            replacements = replacementArrayIndex == -1 ? [] : ParseListEntries(parts[replacementArrayIndex]);
            overlays = overlayArrayIndex == -1 ? [] : ParseListEntries(parts[overlayArrayIndex]);
            effects = effectArrayIndex == -1 ? [] : ParseListEntries(parts[effectArrayIndex]);

            return true;
        }

        void ParseGlobalParamer(string line, Action<int, string> errorLogger)
        {
            if (line.Trim().StartsWith("%id "))
            {
                pack.Id = line.Trim()["%id ".Length..].Trim();
            }
            else if (line.Trim().StartsWith("%updatescript "))
            {
                pack.UpdateScript = line.Trim()["%updatescript ".Length..].Trim();
            }
            else if (line.Trim().StartsWith("%displayname "))
            {
                pack.DisplayName = line.Trim()["%displayname ".Length..].Trim();
            }
            else if (line.Trim().StartsWith("%enabledbydefault "))
            {
                if (!bool.TryParse(line.Trim()["%enabledbydefault ".Length..].Trim(), out var enabledByDefault))
                {
                    errorLogger(lineNumber, $"Enable state must be either \"true\" or \"false\".");
                    pack.EnabledByDefault = true;
                }
                else
                {
                    pack.EnabledByDefault = enabledByDefault;
                }
            }
            else if (line.Trim().StartsWith("%customclipvolume "))
            {
                var customClipData = line.Trim()["%customclipvolume ".Length..].Split('=');

                if (customClipData.Length != 2)
                {
                    errorLogger(lineNumber, $"Expected %customclipvolume clipName = volume.");
                } 
                else if (customClipData[0].Trim() == "")
                {
                    errorLogger(lineNumber, $"Expected a clip name.");
                }
                else if (!float.TryParse(customClipData[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float clipVolume))
                {
                    errorLogger(lineNumber, $"Volume is not a number.");
                }
                else
                {
                    pack.ClipVolumes[customClipData[0].Trim()] = clipVolume;
                }
            }
            else if (line.Trim().StartsWith("%customclippath "))
            {
                var customClipData = line.Trim()["%customclippath ".Length..].Split('=');

                if (customClipData.Length != 2)
                {
                    errorLogger(lineNumber, $"Expected %customclippath clipName = relative/path/to/clip.");
                } 
                else if (customClipData[0].Trim() == "")
                {
                    errorLogger(lineNumber, $"Expected a clip name.");
                }
                else if (string.IsNullOrEmpty(customClipData[1].Trim()))
                {
                    errorLogger(lineNumber, $"Path is missing or empty.");
                }
                else
                {
                    pack.ClipPaths[customClipData[0].Trim()] = customClipData[1].Trim();
                }
            }
            else
            {
                errorLogger(lineNumber, $"Unrecognized attribute {line.Trim().Substring(1)}.");
            }
        }

        void ParseSimpleRouteFormat(string line, Action<int, string> errorLogger)
        {
            var simpleParts = line.Split(ReplacementSeparator, 3);

            if (simpleParts.Length != 2)
            {
                errorLogger(lineNumber, $"Encountered a malformed route (expected key = value), skipping it.");
                return;
            }

            var fields = simpleParts[1].Split(['/'], 3);

            if (fields.Length > 2)
            {
                errorLogger(lineNumber, $"Too many values defined for a route (expected at most 2), skipping it.");
                return;
            }

            var clipName = simpleParts[0].Trim();
            var replacementName = fields[0].Trim();
            var randomWeight = ModAudio.DefaultWeight;

            if (clipName.Trim() == "" || replacementName.Trim() == "")
            {
                errorLogger(lineNumber, $"Either clip name or replacement was empty for a route, skipping it.");
                return;
            }

            if (fields.Length > 1)
            {
                if (!float.TryParse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture, out randomWeight))
                {
                    errorLogger(lineNumber, $"Couldn't parse random weight {fields[1]} for {clipName} => {replacementName}, defaulting to {ModAudio.DefaultWeight}.");
                }
            }

            pack.Routes.Add(new Route()
            {
                OriginalClips = !clipName.StartsWith(ModAudioGame.AliasNameStart) ? [clipName] : [],
                OriginalClipAliases = clipName.StartsWith(ModAudioGame.AliasNameStart) ? [clipName.Substring(ModAudioGame.AliasNameStart.Length)] : [],
                ReplacementClips = [new() {
                        Name = replacementName
                    }],
                ReplacementWeight = randomWeight,
            });
        }
    }
}
