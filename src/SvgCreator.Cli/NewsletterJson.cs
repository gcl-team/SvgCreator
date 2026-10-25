using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SvgCreator;

namespace SvgCreator.Cli;

/// <summary>
/// Reads the <c>newsletter</c> command's file: a JSON object with camelCase properties, blocks
/// told apart by their <c>"type"</c>, which may come anywhere in the block. A map block's
/// <c>"stops"</c> may be the list itself or the path of a stops file, as the <c>map</c> command
/// reads (see <see cref="TravelStopsJson"/>), relative to the newsletter file; the
/// <c>"theme"</c> may be a built-in theme's name or the path of a theme file (ending in
/// <c>.json</c>), also relative to it. Source-generated, so it needs no reflection under Native AOT.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    AllowOutOfOrderMetadataProperties = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Newsletter))]
[JsonSerializable(typeof(NewsletterTheme))]
internal sealed partial class NewsletterJson : JsonSerializerContext
{
    // The same leniency as the source-generated options, for reading the file as a tree first.
    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static async Task<Newsletter> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        try
        {
            // Read as a tree first, so map blocks that name a stops file can have its stops put in
            // their place; the library then sees a newsletter with every map's stops in it.
            JsonNode? root;
            await using (var stream = file.OpenRead())
            {
                root = await JsonNode.ParseAsync(stream, documentOptions: DocumentOptions, cancellationToken: cancellationToken);
            }

            await InlineStopsFilesAsync(root, file, cancellationToken);
            await InlineThemeFileAsync(root, file, cancellationToken);
            return (root ?? throw new InvalidDataException($"{file.Name}: expected a newsletter object, found null.")).Deserialize(Default.Newsletter)
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

    /// <summary>
    /// Replaces each map block's <c>"stops"</c> that is a path with the array in that file. Anything
    /// else is left for deserializing to judge, so a wrong shape gets the usual message.
    /// </summary>
    private static async Task InlineStopsFilesAsync(JsonNode? root, FileInfo file, CancellationToken cancellationToken)
    {
        if (root is not JsonObject newsletter || Property(newsletter, "blocks") is not JsonArray blocks)
        {
            return;
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is not JsonObject block
                || block["type"] is not JsonValue type || !type.TryGetValue(out string? name) || name != "map"
                || PropertyName(block, "stops") is not { } key
                || block[key] is not JsonValue path || !path.TryGetValue(out string? stopsFile))
            {
                continue;
            }

            var stopsPath = Path.Combine(file.DirectoryName!, stopsFile);
            JsonNode? stops;
            try
            {
                await using var stream = File.OpenRead(stopsPath);
                stops = await JsonNode.ParseAsync(stream, documentOptions: DocumentOptions, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException($"{file.Name}: block {i + 1} (map) can't read its stops file {stopsFile}: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"{stopsFile} (stops for block {i + 1} of {file.Name}): {ex.Message}", ex);
            }

            block[key] = stops as JsonArray
                ?? throw new InvalidDataException($"{stopsFile} (stops for block {i + 1} of {file.Name}): expected a JSON array of stops, as the map command reads.");
        }
    }

    /// <summary>
    /// A theme for <c>--theme</c>: a built-in theme's name, or the path of a theme file (ending in
    /// <c>.json</c>) relative to the current folder.
    /// </summary>
    public static async Task<SvgCreator.NewsletterTheme> ReadThemeAsync(string theme, CancellationToken cancellationToken)
    {
        if (!IsThemeFile(theme))
        {
            return SvgCreator.NewsletterTheme.Named(theme);
        }

        var node = await ReadThemeFileAsync(Path.GetFullPath(theme), theme, cancellationToken);
        try
        {
            return node.Deserialize(Default.NewsletterTheme)!;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{theme}: {ex.Message}", ex);
        }
    }

    // A theme given as a path rather than a built-in name.
    private static bool IsThemeFile(string theme) => theme.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Replaces a <c>"theme"</c> that names a theme file with the object in that file.</summary>
    private static async Task InlineThemeFileAsync(JsonNode? root, FileInfo file, CancellationToken cancellationToken)
    {
        if (root is JsonObject newsletter && PropertyName(newsletter, "theme") is { } key
            && newsletter[key] is JsonValue value && value.TryGetValue(out string? theme) && IsThemeFile(theme))
        {
            newsletter[key] = await ReadThemeFileAsync(Path.Combine(file.DirectoryName!, theme), theme, cancellationToken);
        }
    }

    private static async Task<JsonObject> ReadThemeFileAsync(string path, string name, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonNode.ParseAsync(stream, documentOptions: DocumentOptions, cancellationToken: cancellationToken) as JsonObject
                ?? throw new InvalidDataException($"{name}: expected a theme object, such as {{ \"base\": \"minimal\", \"accentColor\": \"#e4572e\" }}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Can't read the theme file {name}: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{name}: {ex.Message}", ex);
        }
    }

    // Property names are read case-insensitively, as the source-generated options do.
    private static string? PropertyName(JsonObject obj, string name) =>
        obj.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

    private static JsonNode? Property(JsonObject obj, string name) => PropertyName(obj, name) is { } key ? obj[key] : null;

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
