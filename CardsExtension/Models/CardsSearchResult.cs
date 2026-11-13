using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CardsExtension.Models;

/// <summary>
/// Wraps the response from GET /1/search?modelTypes=cards
/// Verified: the top-level response is an object with a "cards" array.
/// </summary>
internal sealed class CardsSearchResult
{
    [JsonPropertyName("cards")]
    public List<CardsCard> Cards { get; init; } = [];
}
