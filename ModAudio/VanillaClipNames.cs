namespace Marioalexsan.ModAudio;

public static partial class VanillaClips
{
    private struct TableRow(string clipName, string clipPath, string description)
    {
        public string Name = clipName;
        public string Path = clipPath;
        public string Description = description;
    }

    internal static void GenerateReferenceFile(string location)
    {
        List<TableRow> rows = [];
        TableRow limits = new("Name", "Path", "Description");

        foreach (var path in Paths)
        {
            var clipName = Path.GetFileNameWithoutExtension(path);

            var description = "";

            if (Documentation.TryGetValue(path, out var documentation) && !string.IsNullOrEmpty(documentation))
                description = documentation;

            rows.Add(new TableRow(clipName, path, description));

            if (limits.Name.Length < clipName.Length)
                limits.Name = clipName;

            if (limits.Path.Length < path.Length)
                limits.Path = path;

            if (limits.Description.Length < description.Length)
                limits.Description = description;
        }

        var allLines = rows.Select(x => $"| {x.Name.PadRight(limits.Name.Length)} | {x.Path.PadRight(limits.Path.Length)} | {x.Description.PadRight(limits.Description.Length)} |");

        var fileData = string.Join(Environment.NewLine, [
            "# Vanilla Clips Reference",
            "This file serves as a reference for all of the available audio clip names within the game.",
            "",
            "",
            $"| {"Name".PadRight(limits.Name.Length)} | {"Path".PadRight(limits.Path.Length)} | {"Description".PadRight(limits.Description.Length)} |",
            $"| {new string('-', limits.Name.Length)} | {new string('-', limits.Path.Length)} | {new string('-', limits.Description.Length)} |",
            .. allLines
            ]);

        File.WriteAllText(location, fileData);
    }
}
