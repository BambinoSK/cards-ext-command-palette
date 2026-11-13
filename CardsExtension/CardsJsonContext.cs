using System.Collections.Generic;
using System.Text.Json.Serialization;
using CardsExtension.Models;

namespace CardsExtension;

/// <summary>
/// AOT-safe System.Text.Json source-generation context.
/// Required because the project uses PublishTrimmed=true and IsAotCompatible=true.
/// All types that are deserialized from API responses must be registered here.
/// </summary>
[JsonSerializable(typeof(List<CardsBoard>))]
[JsonSerializable(typeof(List<CardsList>))]
[JsonSerializable(typeof(List<CardsCard>))]
[JsonSerializable(typeof(List<CardsLabel>))]
[JsonSerializable(typeof(List<CardsChecklist>))]
[JsonSerializable(typeof(List<CardsCheckItem>))]
[JsonSerializable(typeof(List<CardsMember>))]
[JsonSerializable(typeof(List<CardsComment>))]
[JsonSerializable(typeof(List<CardsAttachment>))]
[JsonSerializable(typeof(CardsSearchResult))]
[JsonSerializable(typeof(CardsBoard))]
[JsonSerializable(typeof(CardsList))]
[JsonSerializable(typeof(CardsCard))]
[JsonSerializable(typeof(CardsLabel))]
[JsonSerializable(typeof(CardsChecklist))]
[JsonSerializable(typeof(CardsCheckItem))]
[JsonSerializable(typeof(CardsMember))]
[JsonSerializable(typeof(CardsComment))]
[JsonSerializable(typeof(CardsAttachment))]
[JsonSerializable(typeof(CardsCommentData))]
[JsonSerializable(typeof(CardsOAuthTokens))]
[JsonSerializable(typeof(CardsOAuthTokenResponse))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class CardsJsonContext : JsonSerializerContext
{
}
