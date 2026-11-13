// Pin Id encoding tests: board/list names are embedded in stable pin ids
// ("cards.board.{id}|{name}", "cards.list.{id}|{name}|{board}") and are
// URL-encoded so a name containing '|' cannot corrupt the Split('|', n)
// parsing in CardsExtensionCommandsProvider.GetCommandItem.

using Xunit;

namespace CardsExtension.Tests;

public class PinIdEncodingTests
{
    [Theory]
    [InlineData("First board")]
    [InlineData("Board | with | pipes")]
    [InlineData("Number (v2) access")]
    [InlineData("  leading and trailing  ")]
    [InlineData("")]
    public void RoundTrip_PreservesName(string name)
    {
        var encoded = CardsExtensionCommandsProvider.EncodeIdName(name);
        Assert.DoesNotContain('|', encoded);
        Assert.Equal(name, CardsExtensionCommandsProvider.DecodeIdName(encoded));
    }

    [Fact]
    public void Encode_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CardsExtensionCommandsProvider.EncodeIdName(null!));
        Assert.Equal(string.Empty, CardsExtensionCommandsProvider.DecodeIdName(null!));
    }

    [Fact]
    public void Decode_LegacyUnencodedValue_PassesThrough()
    {
        // Pin ids created before encoding was introduced stored the raw name.
        Assert.Equal("First board", CardsExtensionCommandsProvider.DecodeIdName("First board"));
    }

    [Fact]
    public void BoardPinId_WithPipeInName_SplitAndDecodeRecoversName()
    {
        var name = "Sprint | 42";
        var id = $"cards.board.abc123|{CardsExtensionCommandsProvider.EncodeIdName(name)}";

        var parts = id.Split('|', 2);

        Assert.Equal("cards.board.abc123", parts[0]);
        Assert.Equal(name, CardsExtensionCommandsProvider.DecodeIdName(parts[1]));
    }

    [Fact]
    public void ListPinId_WithPipesInListAndBoardNames_SplitAndDecodeRecoversBoth()
    {
        var listName = "To | Do";
        var boardName = "Project | Status";
        var id = $"cards.list.list1|{CardsExtensionCommandsProvider.EncodeIdName(listName)}|{CardsExtensionCommandsProvider.EncodeIdName(boardName)}";

        var parts = id.Split('|', 3);

        Assert.Equal("cards.list.list1", parts[0]);
        Assert.Equal(listName, CardsExtensionCommandsProvider.DecodeIdName(parts[1]));
        Assert.Equal(boardName, CardsExtensionCommandsProvider.DecodeIdName(parts[2]));
    }
}
