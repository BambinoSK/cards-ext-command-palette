using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CardsExtension.Models;

/// <summary>
/// Lightweight snapshot of a card.
/// Fields sourced from GET /1/lists/{listId}/cards and GET /1/search?modelTypes=cards
/// </summary>
internal sealed class CardsCard
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = string.Empty;

    [JsonPropertyName("idList")]
    public string IdList { get; init; } = string.Empty;

    [JsonPropertyName("idBoard")]
    public string IdBoard { get; init; } = string.Empty;

    /// <summary>Due date in ISO 8601 format; null when not set.</summary>
    [JsonPropertyName("due")]
    public string? Due { get; init; }

    /// <summary>True when the due date has been marked as complete.</summary>
    [JsonPropertyName("dueComplete")]
    public bool DueComplete { get; set; }

    /// <summary>True when the card is archived.</summary>
    [JsonPropertyName("closed")]
    public bool Closed { get; init; }

    /// <summary>
    /// Position of the card within its list.
    /// Used by move-up/down logic to compute midpoint positions.
    /// Requires "pos" in the fields= query parameter on GET /1/lists/{id}/cards.
    /// </summary>
    [JsonPropertyName("pos")]
    public double Pos { get; set; }

    /// <summary>Short URL, e.g.</summary>
    [JsonPropertyName("shortUrl")]
    public string ShortUrl { get; init; } = string.Empty;

    /// <summary>Full URL including board slug.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Labels attached to this card.
    /// Requires "labels" in the fields= query parameter.
    /// </summary>
    [JsonPropertyName("labels")]
    public List<CardsLabel> Labels { get; init; } = [];

    // ── Computed display helpers ─────────────────────────────────────────────

    /// <summary>True when the card has a due date that is in the past and not yet completed.</summary>
    public bool IsOverdue
    {
        get
        {
            if (DueComplete || string.IsNullOrEmpty(Due)) return false;
            return DateTime.TryParse(Due, out var due) && due < DateTime.UtcNow;
        }
    }

    /// <summary>Human-readable due-date label (date only) for display in subtitles.</summary>
    public string DueDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Due)) return string.Empty;
            if (!DateTime.TryParse(Due, out var due)) return string.Empty;
            var local = due.ToLocalTime();
            return DueComplete
                ? $"✓ Due {local:MMM d}"
                : IsOverdue
                    ? $"⚠ Overdue {local:MMM d}"
                    : $"Due {local:MMM d}";
        }
    }

    /// <summary>Human-readable due-date label (date + time) for display in the detail panel.</summary>
    public string DueDisplayWithTime
    {
        get
        {
            if (string.IsNullOrEmpty(Due)) return string.Empty;
            if (!DateTime.TryParse(Due, out var due)) return string.Empty;
            var local = due.ToLocalTime();
            return DueComplete
                ? $"✓ Due {local:MMM d, h:mm tt}"
                : IsOverdue
                    ? $"⚠ Overdue {local:MMM d, h:mm tt}"
                    : $"Due {local:MMM d, h:mm tt}";
        }
    }
}
