namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// User feedback (2026-11-14): every real fix this project has made to Director prompt guidance so far
/// meant hand-editing a new paragraph into <c>DirectorPromptBuilder.cs</c>'s own growing C# string
/// literals -- no structure, no way to tell which guidance applies to which kind of song/layout, and no
/// way to add a new lesson without touching C# and rebuilding. This loader reads that guidance instead
/// from plain Markdown files (a tiny hand-rolled frontmatter block, no new dependency -- confirmed
/// nothing in this solution parses Markdown/YAML today), baked into this assembly as
/// <c>EmbeddedResource</c>s the same way <see cref="MultiLayerTemplateCatalogLoader"/>'s own seed JSON
/// is (see that type's own doc comment for why embedded, not loose <c>Content</c>), with the same
/// seed+loose-merge convention that loader already established for a corpus-mined addition: a loose
/// <c>.md</c> file dropped under <c>&lt;knowledgeDirectory&gt;/director-lessons/</c> becomes addable
/// without a code change or rebuild -- the concrete mechanism for "progressively add lessons from real
/// feedback" this phase exists to deliver.
/// </summary>
public static class DirectorLessonCatalogLoader
{
    // Confirmed against the real built assembly: MSBuild sanitizes the "director-lessons" DIRECTORY
    // segment's hyphen into an underscore for the manifest resource name (a directory segment must
    // form a valid identifier component), but leaves each file's own hyphenated name untouched --
    // "SeedData/director-lessons/hold-layer-not-dead.md" becomes
    // "XlightsAI.Studio.Knowledge.SeedData.director_lessons.hold-layer-not-dead.md", not
    // ".director-lessons.".
    private const string ResourceNamespace = "XlightsAI.Studio.Knowledge.SeedData.director_lessons.";
    private const string LooseSubdirectory = "director-lessons";

    private static readonly Lazy<DirectorLessonCatalog> Cached = new(LoadFromEmbeddedResources);

    /// <summary>The shippable, hand-curated seed lessons alone, parsed once and cached for the process lifetime.</summary>
    public static DirectorLessonCatalog Load() => Cached.Value;

    /// <summary>
    /// The same seed lessons as <see cref="Load()"/>, merged with any loose <c>.md</c> lesson files
    /// under <c>&lt;knowledgeDirectory&gt;/director-lessons/</c> -- unlike
    /// <see cref="MultiLayerTemplateCatalogLoader.Load(string?)"/>'s purely-additive merge, a loose
    /// lesson sharing a seed lesson's own <see cref="DirectorLesson.Id"/> WINS (replaces it) rather than
    /// being skipped as a duplicate -- the real intended use is correcting/refining a shipped lesson
    /// in place, not just adding new ones, without waiting for a code change. A missing directory,
    /// missing file, or a loose file that fails to parse is never fatal -- falls back to (or keeps) the
    /// seed version, same disclosed-fallback discipline <see cref="MultiLayerTemplateCatalogLoader"/>
    /// already uses for a corrupt mined file.
    /// </summary>
    public static DirectorLessonCatalog Load(string? knowledgeDirectory)
    {
        var seed = Load();
        if (knowledgeDirectory is null)
        {
            return seed;
        }

        var looseDirectory = Path.Combine(knowledgeDirectory, LooseSubdirectory);
        if (!Directory.Exists(looseDirectory))
        {
            return seed;
        }

        var byId = seed.Lessons.ToDictionary(l => l.Id, StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(looseDirectory, "*.md"))
        {
            DirectorLesson lesson;
            try
            {
                lesson = ParseLesson(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path));
            }
            catch (FormatException)
            {
                continue; // A malformed loose lesson file must never break a real generation.
            }

            byId[lesson.Id] = lesson;
        }

        return new DirectorLessonCatalog(byId.Values.OrderBy(l => l.Priority).ThenBy(l => l.Id, StringComparer.Ordinal).ToList());
    }

    private static DirectorLessonCatalog LoadFromEmbeddedResources()
    {
        var assembly = typeof(DirectorLessonCatalogLoader).Assembly;
        var lessons = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourceNamespace, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded resource '{name}' could not be opened.");
                using var reader = new StreamReader(stream);
                var idFromResourceName = name[ResourceNamespace.Length..^".md".Length];
                return ParseLesson(idFromResourceName, reader.ReadToEnd());
            })
            .OrderBy(l => l.Priority)
            .ThenBy(l => l.Id, StringComparer.Ordinal)
            .ToList();

        if (lessons.Count == 0)
        {
            throw new InvalidOperationException(
                $"No embedded Director lesson resources found under '{ResourceNamespace}*.md' in assembly '{assembly.FullName}'. " +
                "Confirm SeedData/director-lessons/*.md is still declared as an <EmbeddedResource> in XlightsAI.Studio.Knowledge.csproj.");
        }

        return new DirectorLessonCatalog(lessons);
    }

    /// <summary>
    /// A tiny hand-rolled frontmatter parser -- not a Markdown/YAML library, just a leading
    /// <c>---</c>-delimited <c>key: value</c> block (three required keys: <c>id</c>, <c>applies</c>,
    /// <c>priority</c>) followed by the lesson's own real prompt text verbatim. Throws
    /// <see cref="FormatException"/> for anything malformed -- the caller decides whether that is fatal
    /// (an embedded seed lesson shipping broken is a real bug) or recoverable (a loose user-dropped file
    /// with a typo should never break a real generation).
    /// </summary>
    private static DirectorLesson ParseLesson(string fallbackId, string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---")
        {
            throw new FormatException($"Lesson '{fallbackId}' does not start with a '---' frontmatter block.");
        }

        string? id = null;
        string? applies = null;
        int? priority = null;

        var i = 1;
        for (; i < lines.Length && lines[i].Trim() != "---"; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "id": id = value; break;
                case "applies": applies = value; break;
                case "priority": priority = int.TryParse(value, out var p) ? p : throw new FormatException($"Lesson '{fallbackId}' has a non-numeric priority '{value}'."); break;
            }
        }

        if (i >= lines.Length)
        {
            throw new FormatException($"Lesson '{fallbackId}' frontmatter block is never closed with a second '---'.");
        }

        if (id is null || applies is null || priority is null)
        {
            throw new FormatException($"Lesson '{fallbackId}' frontmatter is missing one of the required keys: id, applies, priority.");
        }

        var body = string.Join('\n', lines.Skip(i + 1)).Trim();
        if (body.Length == 0)
        {
            throw new FormatException($"Lesson '{fallbackId}' has no body text after its frontmatter block.");
        }

        return new DirectorLesson(id, applies, priority.Value, body);
    }
}
