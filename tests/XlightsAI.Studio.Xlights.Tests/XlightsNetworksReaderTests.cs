namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-07 (build-plan "The harness") channel-to-model mapping. Fixture shape mirrors the real
/// <c>C:\xLights</c> house's own <c>xlights_networks.xml</c> (three Ethernet controllers, each one
/// 510-channel universe per <c>&lt;network&gt;</c> entry) -- enough universes to prove cross-controller
/// cumulative offsetting, not a full copy of the real (153-line) file.
/// </summary>
public sealed class XlightsNetworksReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-networks-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteShow(string networksXml)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "xlights_networks.xml"), networksXml);
        return _root;
    }

    private const string TwoControllerThreeUniverseFixture = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Networks computer="TEST" GlobalFPPProxy="" GlobalForceLocalIP="" AutoUpdateFromBase="0" BaseShowDir="">
          <Controller Id="1" Name="Ethernet_1" IP="192.168.86.50" Protocol="E131">
            <network ComPort="192.168.86.50" BaudRate="101" NetworkType="E131" MaxChannels="510"/>
            <network ComPort="192.168.86.50" BaudRate="102" NetworkType="E131" MaxChannels="510"/>
          </Controller>
          <Controller Id="2" Name="Ethernet_2" IP="192.168.86.51" Protocol="E131">
            <network ComPort="192.168.86.51" BaudRate="201" NetworkType="E131" MaxChannels="300"/>
          </Controller>
        </Networks>
        """;

    [Fact]
    public void Read_ComputesCumulativeGlobalOffsetsInDocumentOrder()
    {
        var showDirectory = WriteShow(TwoControllerThreeUniverseFixture);

        var ranges = XlightsNetworksReader.Read(showDirectory);

        Assert.Equal(3, ranges.Count);
        Assert.Equal(new UniverseChannelRange("192.168.86.50", 101, 0, 510), ranges[0]);
        Assert.Equal(new UniverseChannelRange("192.168.86.50", 102, 510, 510), ranges[1]);
        Assert.Equal(new UniverseChannelRange("192.168.86.51", 201, 1020, 300), ranges[2]);
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        Directory.CreateDirectory(_root);
        Assert.Throws<FileNotFoundException>(() => XlightsNetworksReader.Read(_root));
    }

    [Fact]
    public void Read_NetworkMissingMaxChannels_ThrowsMalformed()
    {
        var showDirectory = WriteShow("""
            <Networks>
              <Controller Id="1" Name="Ethernet_1" IP="192.168.86.50" Protocol="E131">
                <network ComPort="192.168.86.50" BaudRate="101" NetworkType="E131"/>
              </Controller>
            </Networks>
            """);

        Assert.Throws<XlightsNetworksMalformedException>(() => XlightsNetworksReader.Read(showDirectory));
    }
}
