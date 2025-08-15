using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Tomlet.Attributes;

namespace Marioalexsan.ModAudio;

public class PackScripts
{
    /// <summary>
    /// The name of a hook that should run on every audio engine update.
    /// </summary>
    [JsonProperty("update", Required = Required.DisallowNull)]
    [TomlProperty("update")]
    public string Update { get; set; } = "";

}
