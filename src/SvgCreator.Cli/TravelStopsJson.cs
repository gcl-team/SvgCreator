using System.Text.Json;
using System.Text.Json.Serialization;
using SvgCreator;

namespace SvgCreator.Cli;

/// <summary>
/// Reads the <c>map</c> command's stops file: a JSON array of stops with camelCase properties.
/// Source-generated, so it needs no reflection under Native AOT.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<TravelStop>))]
internal sealed partial class TravelStopsJson : JsonSerializerContext
{
    public static async Task<List<TravelStop>> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        try
        {
            return await JsonSerializer.DeserializeAsync(stream, Default.ListTravelStop, cancellationToken)
                ?? throw new InvalidDataException($"{file.Name}: expected a JSON array of stops, found null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{file.Name}: {ex.Message}", ex);
        }
    }
}
