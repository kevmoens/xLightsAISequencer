namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.4 (effect-family concentration advisory). Per the task's own requirement
/// to "ground it in real xLights effect categories... or in actually-mined corpus co-occurrence data, not
/// a guessed label": this scans the real, author-created corpus (<c>C:\xLights\*.xsq</c>, the same 35 real
/// files 30.0/30.1 used), via <see cref="EffectAlignmentParser"/> (structural XML parsing only, no audio
/// needed), and builds real "substitution family" clusters -- two effect names are unioned into the same
/// family when a real show places them, at different times, in the exact same (Model, EffectLayer index)
/// slot, i.e. a real sequence author used them interchangeably to fill the same structural role.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EffectFamilyCorpusMiningTests
{
    private const string CorpusDirectory = @"C:\xLights";

    [Fact]
    public async Task RealCorpus_MinesEffectFamilyClusters_AndRecordsEvidence()
    {
        if (!Directory.Exists(CorpusDirectory))
        {
            return;
        }

        var xsqFiles = Directory.GetFiles(CorpusDirectory, "*.xsq", SearchOption.TopDirectoryOnly);
        if (xsqFiles.Length == 0)
        {
            return;
        }

        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }
        void Union(string a, string b)
        {
            var ra = Find(a); var rb = Find(b);
            if (ra != rb) parent[ra] = rb;
        }

        var edgeEvidence = new Dictionary<(string, string), int>();

        foreach (var path in xsqFiles)
        {
            IReadOnlyList<ParsedEffectInstance> instances;
            try { instances = EffectAlignmentParser.Parse(path); }
            catch (Exception) { continue; }

            foreach (var slot in instances.GroupBy(i => (i.ModelName, i.LayerIndex)))
            {
                var names = slot.Select(i => i.EffectName).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
                foreach (var n in names) Find(n);
                if (names.Count != 2)
                {
                    continue; // Only a clean A/B substitution slot counts -- a hub slot hosting many different effects over a song's life is a generic base layer, not real family evidence.
                }

                var key = (names[0], names[1]);
                edgeEvidence[key] = edgeEvidence.GetValueOrDefault(key) + 1;
            }
        }

        const int minEdgeEvidence = 10;
        foreach (var (key, count) in edgeEvidence)
        {
            if (count >= minEdgeEvidence)
            {
                Union(key.Item1, key.Item2);
            }
        }

        var clusters = parent.Keys.GroupBy(Find).OrderByDescending(g => g.Count()).ToList();

        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence");
        Directory.CreateDirectory(evidenceDir);
        var lines = clusters.Select(c => $"- ({c.Count()} effects): {string.Join(", ", c.OrderBy(n => n, StringComparer.Ordinal))}");
        await File.WriteAllTextAsync(Path.Combine(evidenceDir, "PHASE-30-FAMILY-CONCENTRATION-MINING.md"), $"""
            # Phase 30 step 30.4 -- real-corpus-mined effect-family clusters (scratch output)

            {xsqFiles.Length} real files scanned from `{CorpusDirectory}`. {parent.Keys.Count} distinct real
            effect names observed across any (Model, EffectLayer) slot. {clusters.Count} clusters found via
            same-slot substitution union-find.

            {string.Join("\n", lines)}

            Top edges by evidence count:
            {string.Join("\n", edgeEvidence.OrderByDescending(kv => kv.Value).Take(40).Select(kv => $"- {kv.Key.Item1} <-> {kv.Key.Item2}: {kv.Value}"))}
            """, CancellationToken.None);

        Assert.True(clusters.Count >= 1);
    }
}
