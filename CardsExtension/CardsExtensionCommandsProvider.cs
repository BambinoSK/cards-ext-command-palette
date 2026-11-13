using System;
using CardsExtension.Pages;
using CardsExtension.Services;
using CardsExtension.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension;

/// <summary>
/// Root CommandProvider for the extension.
///
/// TopLevelCommands(): returns the single board-list entry.
///   MoreCommands on the top-level item exposes the Settings page.
///
/// Settings:
///   Wired via CommandProvider.Settings = _settingsManager.Settings.
///   The settings page is automatically accessible in the CmdPal extension manager.
///
/// GetCommandItem(string id):
///   Enables the Command Palette host's built-in Pin mechanism for nested commands.
///   When a user right-clicks a list's CardsPage and pins it, Command Palette calls
///   GetCommandItem(id) to restore it after restart. The item then appears in the
///   global "Pinned" section at the top of Command Palette.
///
///   Id format: "cards.list.{listId}|{listName}|{boardName}"
/// </summary>
public partial class CardsExtensionCommandsProvider : CommandProvider
{
    private static readonly IconInfo CardsIcon =
        IconHelpers.FromRelativePaths(@"Assets\StoreLogo.png", @"Assets\StoreLogo.png");

    private readonly CardsService _cardsService = new();
    private readonly CardsSettingsManager _settingsManager = CardsSettingsManager.Instance;
    private readonly BoardListPage _boardListPage;
    private readonly ICommandItem[] _commands;

    public CardsExtensionCommandsProvider()
    {
        DisplayName = ExtensionIdentity.DisplayName;
        Icon = CardsIcon;
        Id = ExtensionIdentity.ProviderId;

        _boardListPage = new BoardListPage(_cardsService);

        _commands =
        [
            new CommandItem(_boardListPage)
            {
                Title = ExtensionIdentity.DisplayName,
                Subtitle = "Browse boards, lists, and cards - create, move, archive",
                Icon = CardsIcon,
                MoreCommands =
                [
                    new CommandContextItem(_settingsManager.Settings.SettingsPage)
                    {
                        Title = "Settings",
                    },
                ],
            },
        ];

        Settings = _settingsManager.Settings;
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    /// <summary>
    /// Enables the host's built-in Pin mechanism for nested commands.
    /// Called by Command Palette when it needs to restore a pinned item by its stable Id.
    ///
    /// Supported Id formats (names are URL-encoded so a board/list name
    /// containing '|' cannot corrupt the id|name|board split - see
    /// EncodeIdName/DecodeIdName):
    ///   "cards.board.{boardId}|{boardName}"    - ListsPage for a board (set in BoardListPage)
    ///   "cards.list.{listId}|{listName}|{boardName}" - CardsPage for a list (set in ListsPage)
    /// </summary>
    public override ICommandItem? GetCommandItem(string id)
    {
        // ── Pinned board: "cards.board.{boardId}|{boardName}" ──────────────
        if (id.StartsWith("cards.board.", System.StringComparison.Ordinal))
        {
            var boardPayload = id["cards.board.".Length..];
            var boardParts = boardPayload.Split('|', 2);

            var boardId   = boardParts[0];
            var boardName = boardParts.Length > 1 ? DecodeIdName(boardParts[1]) : "Cards";

            var board = new Models.CardsBoard { Id = boardId, Name = boardName };
            var boardPage = new Pages.ListsPage(_cardsService, board);
            boardPage.Id = id;

            return new CommandItem(boardPage)
            {
                Title = boardName,
                Subtitle = "Cards",
                Icon = CardsIcon,
            };
        }

        // ── Pinned list: "cards.list.{listId}|{listName}|{boardName}" ──────
        if (!id.StartsWith("cards.list.", System.StringComparison.Ordinal))
            return null;

        // Parse: "cards.list.{listId}|{listName}|{boardName}"
        var listPayload = id["cards.list.".Length..];
        var listParts = listPayload.Split('|', 3);

        var listId   = listParts[0];
        var listName = listParts.Length > 1 ? DecodeIdName(listParts[1]) : listId;
        var listBoardName = listParts.Length > 2 ? DecodeIdName(listParts[2]) : "Cards";

        var list = new Models.CardsList { Id = listId, Name = listName };
        var listPage = new CardsPage(_cardsService, list, []);
        listPage.Id = id;

        return new CommandItem(listPage)
        {
            Title = listName,
            Subtitle = listBoardName,
            Icon = CardsIcon,
        };
    }

    /// <summary>
    /// Encodes a board/list name for embedding in a stable pin Id
    /// ("cards.board.{id}|{name}" / "cards.list.{id}|{name}|{board}") so a
    /// name containing '|' cannot corrupt the split in GetCommandItem.
    /// URL-encoding ('%' + hex) never emits a raw '|' or '%'-free ambiguity.
    /// </summary>
    internal static string EncodeIdName(string name) =>
        Uri.EscapeDataString(name ?? string.Empty);

    /// <summary>Reverses EncodeIdName; tolerant of legacy (unencoded) ids.</summary>
    internal static string DecodeIdName(string encoded) =>
        Uri.UnescapeDataString(encoded ?? string.Empty);
}
