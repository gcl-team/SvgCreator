using System.Text.Json;
using System.Text.Json.Serialization;
using SvgCreator;

namespace SvgCreator.Cli;

/// <summary>
/// Reads the <c>newsletter</c> command's file: a JSON object with camelCase properties, blocks
/// told apart by their <c>"type"</c>, which may come anywhere in the block. Source-generated, so
/// it needs no reflection under Native AOT.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    AllowOutOfOrderMetadataProperties = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Newsletter))]
internal sealed partial class NewsletterJson : JsonSerializerContext
{
    public static async Task<Newsletter> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        try
        {
            return await JsonSerializer.DeserializeAsync(stream, Default.Newsletter, cancellationToken)
                ?? throw new InvalidDataException($"{file.Name}: expected a newsletter object, found null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{file.Name}: {Describe(ex)}", ex);
        }
        catch (NotSupportedException ex) when (ex.Message.Contains("type discriminator", StringComparison.OrdinalIgnoreCase))
        {
            // What System.Text.Json says for a block with no "type": it can't make an abstract block.
            throw new InvalidDataException($"{file.Name}: every block needs a \"type\" (ribbon, text, cards, image, button, stats, callout or map){Where(ex.Message)}.", ex);
        }
    }

    // The JSON path System.Text.Json puts in its messages ("... Path: $.blocks[0] | LineNumber: ..."), if any.
    private static string Where(string message)
    {
        var start = message.IndexOf("Path: ", StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        start += "Path: ".Length;
        var end = message.IndexOf(" |", start, StringComparison.Ordinal);
        return $"; missing at {(end < 0 ? message[start..] : message[start..end])}";
    }

    // An unknown "type" comes back as a message about type discriminators; say it in the file's terms.
    private static string Describe(JsonException ex) =>
        ex.Message.Contains("type discriminator", StringComparison.OrdinalIgnoreCase)
            ? $"unknown block \"type\" at {ex.Path}. Use ribbon, text, cards, image, button, stats, callout or map."
            : ex.Message;
}
