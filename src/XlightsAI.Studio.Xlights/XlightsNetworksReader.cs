using System.Xml;
using System.Xml.Linq;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// One real controller output's own declared channel range within <c>xlights_networks.xml</c>'s own
/// cumulative universe ordering -- the house's configured DMX/E1.31 output capacity, not a rendered
/// sequence's own channel layout (see remarks on <see cref="XlightsNetworksReader"/>).
/// </summary>
/// <param name="ControllerIp">The owning controller's <c>IP</c>/<c>ComPort</c> value -- the key a model's own <c>StartChannel</c>/<c>StringN</c> attribute references.</param>
/// <param name="Universe">The network's own <c>BaudRate</c> value (xLights' own field name for a universe/port number under E1.31, despite the name).</param>
/// <param name="GlobalChannelOffset">0-based channel index of this universe's first channel within the house's own declared network capacity, i.e. the sum of every earlier universe's <see cref="ChannelCount"/> in document order.</param>
/// <param name="ChannelCount">This universe's own declared <c>MaxChannels</c>.</param>
public sealed record UniverseChannelRange(string ControllerIp, int Universe, long GlobalChannelOffset, int ChannelCount);

/// <summary>
/// Default reader for <c>xlights_networks.xml</c>: the house's own declared controller/universe/channel-
/// capacity configuration, independently useful (e.g. a future controller/output inspection or
/// configuration-validation feature) and correctly, separately verified against the real
/// <c>C:\xLights</c> house.
///
/// <para>
/// <b>Not the mechanism for decoding a rendered <c>.fseq</c>'s channel layout.</b> An earlier version of
/// this PHASE-07 (build-plan "The harness") work assumed a rendered sequence's channel buffer was
/// addressed by this file's own absolute, house-wide cumulative universe offsets combined with each
/// model's <c>StartChannel</c>/<c>StringN</c> attribute. That was directly disproven: a real headless
/// render of just the "Arches" model group (4 real 100-node members) produced a decoded FSEQ with
/// exactly 1200 channels (4&#215;100&#215;3) -- far smaller than any of those models' real absolute
/// universe addresses would imply. The real mechanism (confirmed, see <see cref="SequenceChannelLayoutResolver"/>)
/// is a per-sequence locally-compacted buffer built from the actual rendered <c>.xsq</c>'s own
/// <c>&lt;DisplayElements&gt;</c> order, independent of this file entirely.
/// </para>
/// </summary>
public static class XlightsNetworksReader
{
    private const long MaxDocumentCharacters = 5_000_000;

    /// <exception cref="FileNotFoundException"><c>xlights_networks.xml</c> does not exist under <paramref name="showDirectory"/>.</exception>
    /// <exception cref="XlightsNetworksMalformedException">The file is not safely parseable, or a <c>&lt;Controller&gt;</c>/<c>&lt;network&gt;</c> element is missing a required attribute.</exception>
    public static IReadOnlyList<UniverseChannelRange> Read(string showDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showDirectory);

        var path = Path.Combine(Path.GetFullPath(showDirectory), ShowDirectoryFileNames.Networks);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Required file '{ShowDirectoryFileNames.Networks}' was not found under '{showDirectory}'.", path);
        }

        var bytes = File.ReadAllBytes(path);
        var document = ParseSafely(bytes, path);

        var root = document.Root ?? throw new XlightsNetworksMalformedException($"'{path}' has no root element.");

        var ranges = new List<UniverseChannelRange>();
        var cumulativeOffset = 0L;

        foreach (var controller in root.Elements("Controller"))
        {
            var ip = (string?)controller.Attribute("IP");
            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new XlightsNetworksMalformedException($"'{path}' has a <Controller> with no IP attribute.");
            }

            foreach (var network in controller.Elements("network"))
            {
                var universeText = (string?)network.Attribute("BaudRate");
                var maxChannelsText = (string?)network.Attribute("MaxChannels");
                if (!int.TryParse(universeText, out var universe))
                {
                    throw new XlightsNetworksMalformedException(
                        $"'{path}': controller '{ip}' has a <network> with a missing/non-numeric BaudRate (universe) attribute.");
                }

                if (!int.TryParse(maxChannelsText, out var channelCount) || channelCount <= 0)
                {
                    throw new XlightsNetworksMalformedException(
                        $"'{path}': controller '{ip}' universe {universe} has a missing/non-positive MaxChannels attribute.");
                }

                ranges.Add(new UniverseChannelRange(ip, universe, cumulativeOffset, channelCount));
                cumulativeOffset += channelCount;
            }
        }

        return ranges;
    }

    private static XDocument ParseSafely(byte[] bytes, string sourcePath)
    {
        using var byteStream = new MemoryStream(bytes);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaxDocumentCharacters,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var reader = XmlReader.Create(byteStream, settings);
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new XlightsNetworksMalformedException($"'{sourcePath}' is not a safely parseable XML document: {ex.Message}", ex);
        }
    }
}
