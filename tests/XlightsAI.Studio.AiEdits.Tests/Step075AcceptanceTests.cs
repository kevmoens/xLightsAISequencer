using System.Net;
using System.Text;
using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits.Tests;

/// <summary>
/// PHASE-11 Step 075 ("Add optional Ollama/cloud provider behind opt-in structured-output adapter").
/// Required acceptance, verbatim: "Provider off means no outbound calls; malformed JSON rejected."
/// Required failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not
/// corrupt the project." Exercises the real <see cref="OptionalAiPlannerFactory"/>,
/// <see cref="NullOptionalAiPlanner"/> and <see cref="HttpStructuredOutputAiPlanner"/> against an
/// in-process <see cref="CountingHandler"/> test double (no real network call, no mocking library --
/// the same convention every prior AiEdits test already follows) and the same gate-demo layout shape
/// (Roofline, Arch 1, Arch 2) every prior PHASE-02/03/11 step's own tests already use.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Step075AcceptanceTests
{
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    private static SequenceDocument GateDemoPlan(DisplayGraph layout) =>
        new ThreeEffectPlanFactory().CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], 300_000_000, new EffectRecipeRegistry());

    private static SelectionContext CaptureArchSelection(SequenceDocument plan, EffectInstance arch1, EffectInstance arch2) =>
        new SelectionContextCaptureService(new EffectLockPolicy()).Capture(
            plan,
            new DocumentRevision(plan.Revision),
            new EditScope(
                new TimeWindow(Math.Min(arch1.StartTicks, arch2.StartTicks), Math.Max(arch1.EndTicks, arch2.EndTicks)),
                new HashSet<ModelId> { arch1.Target, arch2.Target }),
            [], [], [], []);

    private static (SequenceDocument Plan, SelectionContext Context) BuildGateDemoSelection()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        return (plan, CaptureArchSelection(plan, arch1, arch2));
    }

    private static AiProviderOptions EnabledOptions() => new(
        Enabled: true,
        Kind: AiProviderKind.OllamaLocal,
        Endpoint: new Uri("http://localhost:11434/api/generate"),
        ModelName: "test-model",
        RequestTimeout: TimeSpan.FromSeconds(5),
        MaxRetries: 2,
        MaxResponseBytes: 4096);

    private static string ValidEnvelopeJson(SelectionContext context, SequenceDocument plan) =>
        $$"""
        {
          "Explanation": "Rising piano pops across the selected arches.",
          "Operations": [
            {
              "OperationId": "op-1",
              "Kind": "SetBrightness",
              "StartTicks": {{context.Scope.Time.StartTicks}},
              "EndTicks": {{context.Scope.Time.EndTicks}},
              "Targets": ["Arch 1", "Arch 2"],
              "Parameters": { "Level": "90" }
            }
          ]
        }
        """;

    // ---- Required acceptance: "Provider off means no outbound calls." ----------------------------

    [Fact]
    public async Task Step075_Acceptance_ProviderOffMeansNoOutboundCalls()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => throw new InvalidOperationException("No outbound call should ever reach the transport when the provider is disabled."));
        using var httpClient = new HttpClient(handler);

        var planner = OptionalAiPlannerFactory.Create(AiProviderOptions.Disabled, httpClient);

        Assert.IsType<NullOptionalAiPlanner>(planner);
        await Assert.ThrowsAsync<AiProviderDisabledException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(0, handler.CallCount);
        Assert.Equal(3, plan.Effects.Count);
    }

    // ---- Required acceptance: "malformed JSON rejected." ------------------------------------------

    [Fact]
    public async Task Step075_Acceptance_MalformedJsonRejected()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ not valid json ]", Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.MalformedJson, ex.Reason);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(3, plan.Effects.Count);
    }

    [Fact]
    public async Task Step075_Acceptance_UnknownPropertyInResponse_RejectedAsMalformedJson_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{ "Explanation": "x", "Operations": [], "UnexpectedField": "not in the schema" }""",
                Encoding.UTF8,
                "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.MalformedJson, ex.Reason);
    }

    [Fact]
    public async Task ValidResponse_ProducesValidatedProposal()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidEnvelopeJson(context, plan), Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var proposal = await planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None);

        var operation = Assert.Single(proposal.Operations);
        Assert.Equal(StructuredEditOperationKinds.SetBrightness, operation.Kind);
        Assert.Equal(plan.Revision, proposal.BaseRevision);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UnknownOperationKind_IsRejected_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var json = $$"""
        {
          "Explanation": "x",
          "Operations": [
            { "OperationId": "op-1", "Kind": "DeleteEverything",
              "StartTicks": {{context.Scope.Time.StartTicks}}, "EndTicks": {{context.Scope.Time.EndTicks}},
              "Targets": ["Arch 1"], "Parameters": {} }
          ]
        }
        """;
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.UnknownOperationKind, ex.Reason);
    }

    [Fact]
    public async Task OutOfScopeTarget_RooflineOutsideSelectedArches_IsRejected_NegativePath()
    {
        // Phase gate cross-cutting test #3, verbatim: "LLM cannot target Roofline outside selected arches."
        var (plan, context) = BuildGateDemoSelection();
        var json = $$"""
        {
          "Explanation": "x",
          "Operations": [
            { "OperationId": "op-1", "Kind": "SetBrightness",
              "StartTicks": {{context.Scope.Time.StartTicks}}, "EndTicks": {{context.Scope.Time.EndTicks}},
              "Targets": ["Roofline"], "Parameters": { "Level": "100" } }
          ]
        }
        """;
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.OutOfScopeTarget, ex.Reason);
    }

    [Fact]
    public async Task OutOfScopeTime_ExtendingPastSelectedWindow_IsRejected_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var json = $$"""
        {
          "Explanation": "x",
          "Operations": [
            { "OperationId": "op-1", "Kind": "SetBrightness",
              "StartTicks": {{context.Scope.Time.StartTicks}}, "EndTicks": {{context.Scope.Time.EndTicks + 10_000_000}},
              "Targets": ["Arch 1"], "Parameters": { "Level": "100" } }
          ]
        }
        """;
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.OutOfScopeTime, ex.Reason);
    }

    [Fact]
    public async Task EmptyOperations_IsRejected_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "Explanation": "nothing to do", "Operations": [] }""", Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.EmptyOperations, ex.Reason);
    }

    [Fact]
    public async Task ResponseLargerThanConfiguredLimit_IsRejected_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var oversized = new string('x', 10_000);
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{ "Explanation": "{{oversized}}", "Operations": [] }""", Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions() with { MaxResponseBytes = 256 }, httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.ResponseTooLarge, ex.Reason);
    }

    // ---- Required failure/edge case: stale revision must not corrupt the project, no outbound call ----

    [Fact]
    public async Task StaleRevision_IsRejected_BeforeAnyOutboundCall_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var staleDocument = plan with { Revision = plan.Revision + 1 };
        var handler = new CountingHandler(_ => throw new InvalidOperationException("No outbound call should be made for a stale selection."));
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, staleDocument, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.StaleRevision, ex.Reason);
        Assert.Equal(0, handler.CallCount);
    }

    // ---- Required failure/edge case: cancellation must not corrupt the project -------------------

    [Fact]
    public async Task CanceledToken_ThrowsWithoutAnyOutboundCall_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => throw new InvalidOperationException("No outbound call should be made for an already-canceled token."));
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions(), httpClient);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, cts.Token));

        Assert.Equal(0, handler.CallCount);
        Assert.Equal(3, plan.Effects.Count);
    }

    [Fact]
    public async Task ProposeAsync_NullArguments_Throw_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var planner = NullOptionalAiPlanner.Instance;

        await Assert.ThrowsAsync<ArgumentNullException>(() => planner.ProposeAsync(null!, context, plan, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => planner.ProposeAsync("brighten", null!, plan, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => planner.ProposeAsync("brighten", context, null!, CancellationToken.None));
    }

    // ---- Required acceptance: retries ("enforce size/timeouts, retries") ---------------------------

    [Fact]
    public async Task TransientFailure_IsRetried_ThenSucceeds()
    {
        var (plan, context) = BuildGateDemoSelection();
        var attempts = 0;
        var handler = new CountingHandler(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidEnvelopeJson(context, plan), Encoding.UTF8, "application/json"),
            };
        });
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions() with { MaxRetries = 2 }, httpClient);

        var proposal = await planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None);

        Assert.Single(proposal.Operations);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task PersistentTransientFailure_ExhaustsRetries_ThenRejectedAsProviderUnavailable_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var planner = OptionalAiPlannerFactory.Create(EnabledOptions() with { MaxRetries = 1, RequestTimeout = TimeSpan.FromSeconds(2) }, httpClient);

        var ex = await Assert.ThrowsAsync<AiProposalRejectedException>(() =>
            planner.ProposeAsync("rising piano pops", context, plan, CancellationToken.None));

        Assert.Equal(AiProposalRejectionReason.ProviderUnavailable, ex.Reason);
        Assert.Equal(2, handler.CallCount); // initial attempt + 1 retry
    }

    // ---- Settings store round trip and safe fallback -----------------------------------------------

    [Fact]
    public async Task JsonAiProviderSettingsStore_RoundTripsThroughAtomicWrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xlai-step075-settings-" + Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(directory, "ai-provider-settings.json");
        try
        {
            var store = new JsonAiProviderSettingsStore(filePath);
            var settings = new AiProviderSettings(
                Enabled: true,
                Kind: AiProviderKind.CloudByok,
                Endpoint: new Uri("https://example.invalid/v1/generate"),
                ModelName: "gpt-structured",
                CredentialName: "cloud-byok-key",
                RequestTimeoutSeconds: 15,
                MaxRetries: 3,
                MaxResponseBytes: 32_768);

            await store.SaveAsync(settings, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(settings, loaded);
            Assert.DoesNotContain("tmp-", Directory.GetFiles(directory).Single());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task JsonAiProviderSettingsStore_MissingFile_FallsBackToDisabled_NegativePath()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "xlai-step075-settings-" + Guid.NewGuid().ToString("N"), "missing.json");
        var store = new JsonAiProviderSettingsStore(filePath);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AiProviderSettings.Disabled, loaded);
    }

    [Fact]
    public async Task JsonAiProviderSettingsStore_CorruptFile_FallsBackToDisabled_NegativePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xlai-step075-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "ai-provider-settings.json");
        try
        {
            await File.WriteAllTextAsync(filePath, "{ this is not valid json");
            var store = new JsonAiProviderSettingsStore(filePath);

            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(AiProviderSettings.Disabled, loaded);
            Assert.False(loaded.Enabled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ---- Credential store: never plaintext on disk, Windows-guarded --------------------------------

    [Fact]
    public async Task DpapiCredentialStore_RoundTripsEncryptedSecret()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // BLOCKED on non-Windows CI: DPAPI is Windows-only by design, see ICredentialStore remarks.
        }

        var directory = Path.Combine(Path.GetTempPath(), "xlai-step075-cred-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DpapiCredentialStore(directory);
            await store.StoreAsync("cloud-byok-key", "sk-super-secret-value", CancellationToken.None);

            var onDiskBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "cloud-byok-key.cred"));
            var onDiskText = Encoding.UTF8.GetString(onDiskBytes);
            Assert.DoesNotContain("sk-super-secret-value", onDiskText);

            var retrieved = await store.TryGetAsync("cloud-byok-key", CancellationToken.None);
            Assert.Equal("sk-super-secret-value", retrieved);

            await store.DeleteAsync("cloud-byok-key", CancellationToken.None);
            Assert.Null(await store.TryGetAsync("cloud-byok-key", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DpapiCredentialStore_MissingEntry_ReturnsNull_NegativePath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "xlai-step075-cred-" + Guid.NewGuid().ToString("N"));
        var store = new DpapiCredentialStore(directory);

        Assert.Null(await store.TryGetAsync("never-stored", CancellationToken.None));
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_respond(request));
        }
    }
}
