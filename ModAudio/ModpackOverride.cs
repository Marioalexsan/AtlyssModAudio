using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Marioalexsan.ModAudio;

public class ModpackOverride
{
    public enum EnableStates
    {
        /// <summary>
        /// Don't apply any overrides.
        /// </summary>
        NoChanges,
        /// <summary>
        /// The audio pack is overriden to always start enabled on first boot.
        /// </summary>
        EnableByDefault,
        /// <summary>
        /// The audio pack is overriden to always start disabled on first boot.
        /// </summary>
        DisableByDefault,
        /// <summary>
        /// The audio pack is overriden to always start enabled, even if toggled off in-game.
        /// This means that the saved enable state isn't used!
        /// </summary>
        AlwaysEnabled,
        /// <summary>
        /// The audio pack is overriden to always start disabled, even if toggled on in-game.
        /// This means that the saved enable state isn't used!
        /// </summary>
        AlwaysDisabled
    }
    
    public string TargetPackId { get; set; } = "";
    
    [JsonConverter(typeof(StringEnumConverter))]
    public EnableStates? EnableState { get; set; }
}