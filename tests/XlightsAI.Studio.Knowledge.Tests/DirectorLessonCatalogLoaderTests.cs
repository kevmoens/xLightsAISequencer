namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// User feedback (2026-11-14): Director prompt guidance externalized out of
/// <c>DirectorPromptBuilder.cs</c>'s hardcoded C# string literals into plain Markdown lesson files --
/// see <see cref="DirectorLessonCatalogLoader"/>'s own doc comment.
/// </summary>
public sealed class DirectorLessonCatalogLoaderTests
{
    [Fact]
    public void Load_ReturnsTheSeedLessons_IncludingTheMovementFix()
    {
        var catalog = DirectorLessonCatalogLoader.Load();

        Assert.NotEmpty(catalog.Lessons);
        Assert.Contains(catalog.Lessons, l => l.Id == "movement-across-models");
        Assert.Contains(catalog.Lessons, l => l.Id == "negative-space");
        Assert.Contains(catalog.Lessons, l => l.Id == "sweep-across-props");
    }

    [Fact]
    public void Load_EveryLesson_HasARealClosedAppliesTagAndNonEmptyBody()
    {
        var catalog = DirectorLessonCatalogLoader.Load();
        var validTags = new[] { DirectorLesson.AppliesAlways, DirectorLesson.AppliesTreeOrMatrixPresent, DirectorLesson.AppliesMultiModelGroupPresent };

        foreach (var lesson in catalog.Lessons)
        {
            Assert.Contains(lesson.Applies, validTags);
            Assert.False(string.IsNullOrWhiteSpace(lesson.Body));
        }
    }

    [Fact]
    public void Load_MovementAcrossModelsLesson_AppliesToMultiModelGroupPresent_AndIsHighestPriority()
    {
        var catalog = DirectorLessonCatalogLoader.Load();
        var lesson = catalog.Lessons.Single(l => l.Id == "movement-across-models");

        Assert.Equal(DirectorLesson.AppliesMultiModelGroupPresent, lesson.Applies);
        Assert.True(catalog.Lessons.All(l => l.Id == lesson.Id || l.Priority >= lesson.Priority), "The movement fix should be the highest-priority (lowest number) lesson.");
    }

    [Fact]
    public void Load_IsCached_ReturnsTheSameInstanceAcrossCalls()
    {
        var first = DirectorLessonCatalogLoader.Load();
        var second = DirectorLessonCatalogLoader.Load();

        Assert.Same(first, second);
    }

    [Fact]
    public void LoadWithDirectory_NullDirectory_ReturnsSameSeedCatalogAsParameterlessLoad()
    {
        var catalog = DirectorLessonCatalogLoader.Load(knowledgeDirectory: null);

        Assert.Equal(DirectorLessonCatalogLoader.Load().Lessons.Count, catalog.Lessons.Count);
    }

    [Fact]
    public void LoadWithDirectory_NoLooseLessonsDirectory_ReturnsSameSeedCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xlai-director-lesson-loader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var catalog = DirectorLessonCatalogLoader.Load(directory);
            Assert.Equal(DirectorLessonCatalogLoader.Load().Lessons.Count, catalog.Lessons.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadWithDirectory_NewLooseLesson_IsAddedWithoutAnyCodeChange()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xlai-director-lesson-loader-" + Guid.NewGuid().ToString("N"));
        var lessonsDir = Path.Combine(directory, "director-lessons");
        Directory.CreateDirectory(lessonsDir);
        try
        {
            File.WriteAllText(Path.Combine(lessonsDir, "test-new-lesson.md"), "---\nid: test-new-lesson\napplies: always\npriority: 99\n---\nA brand-new real lesson dropped in without touching any C#.");

            var merged = DirectorLessonCatalogLoader.Load(directory);

            Assert.Equal(DirectorLessonCatalogLoader.Load().Lessons.Count + 1, merged.Lessons.Count);
            var added = Assert.Single(merged.Lessons, l => l.Id == "test-new-lesson");
            Assert.Equal(DirectorLesson.AppliesAlways, added.Applies);
            Assert.Equal(99, added.Priority);
            Assert.Equal("A brand-new real lesson dropped in without touching any C#.", added.Body);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadWithDirectory_LooseLessonSharingASeedId_ReplacesTheSeedLesson()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xlai-director-lesson-loader-" + Guid.NewGuid().ToString("N"));
        var lessonsDir = Path.Combine(directory, "director-lessons");
        Directory.CreateDirectory(lessonsDir);
        try
        {
            File.WriteAllText(Path.Combine(lessonsDir, "negative-space.md"), "---\nid: negative-space\napplies: always\npriority: 1\n---\nA refined real replacement for the seed lesson.");

            var merged = DirectorLessonCatalogLoader.Load(directory);

            Assert.Equal(DirectorLessonCatalogLoader.Load().Lessons.Count, merged.Lessons.Count);
            var replaced = Assert.Single(merged.Lessons, l => l.Id == "negative-space");
            Assert.Equal(1, replaced.Priority);
            Assert.Equal("A refined real replacement for the seed lesson.", replaced.Body);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadWithDirectory_MalformedLooseLesson_IsSkipped_NeverBreaksGeneration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xlai-director-lesson-loader-" + Guid.NewGuid().ToString("N"));
        var lessonsDir = Path.Combine(directory, "director-lessons");
        Directory.CreateDirectory(lessonsDir);
        try
        {
            File.WriteAllText(Path.Combine(lessonsDir, "broken.md"), "this is not a frontmatter block at all");

            var merged = DirectorLessonCatalogLoader.Load(directory);

            Assert.Equal(DirectorLessonCatalogLoader.Load().Lessons.Count, merged.Lessons.Count);
            Assert.DoesNotContain(merged.Lessons, l => l.Id == "broken");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
