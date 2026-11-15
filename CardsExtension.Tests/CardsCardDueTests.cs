// Due dates arrive as UTC ISO 8601 strings ("2026-01-31T12:00:00.000Z"); the +/-30 minute
// cases only catch local/UTC mix-ups on machines whose UTC offset is at least 30 minutes.

using System.Globalization;
using CardsExtension.Models;
using Xunit;

namespace CardsExtension.Tests;

public sealed class CardsCardDueTests
{
    private static string Iso(DateTime utc) =>
        utc.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z";

    [Fact]
    public void IsOverdue_DueHalfAnHourAgo_IsTrue()
    {
        var card = new CardsCard { Due = Iso(DateTime.UtcNow.AddMinutes(-30)) };

        Assert.True(card.IsOverdue);
    }

    [Fact]
    public void IsOverdue_DueInHalfAnHour_IsFalse()
    {
        var card = new CardsCard { Due = Iso(DateTime.UtcNow.AddMinutes(30)) };

        Assert.False(card.IsOverdue);
    }

    [Fact]
    public void IsOverdue_ExplicitOffset_IsHonoured()
    {
        // The same two instants, written with a +14:00 offset.
        var offset = TimeSpan.FromHours(14);
        string WithOffset(DateTimeOffset instant) =>
            instant.ToOffset(offset).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        Assert.True(new CardsCard { Due = WithOffset(DateTimeOffset.UtcNow.AddMinutes(-30)) }.IsOverdue);
        Assert.False(new CardsCard { Due = WithOffset(DateTimeOffset.UtcNow.AddMinutes(30)) }.IsOverdue);
    }

    [Fact]
    public void IsOverdue_CompletedCard_IsFalse()
    {
        var card = new CardsCard { Due = Iso(DateTime.UtcNow.AddDays(-2)), DueComplete = true };

        Assert.False(card.IsOverdue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void MissingOrUnparseableDue_IsNotOverdueAndHasNoLabel(string? due)
    {
        var card = new CardsCard { Due = due };

        Assert.False(card.IsOverdue);
        Assert.Equal(string.Empty, card.DueDisplay);
        Assert.Equal(string.Empty, card.DueDisplayWithTime);
    }

    [Fact]
    public void DueDisplay_MarksAnOverdueCard()
    {
        var card = new CardsCard { Due = Iso(DateTime.UtcNow.AddMinutes(-30)) };

        Assert.Contains("Overdue", card.DueDisplay);
        Assert.Contains("Overdue", card.DueDisplayWithTime);
    }
}
