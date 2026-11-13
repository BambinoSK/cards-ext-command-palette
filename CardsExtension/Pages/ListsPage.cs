using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Auth;
using CardsExtension.Models;
using CardsExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Pages;

/// <summary>
/// Shows all open lists (columns) on a board.
/// Selecting a list navigates to CardsPage for that list.
///
/// Each CardsPage is given a stable Id = "cards.list.{listId}" so that
/// Command Palette's built-in Pin mechanism can resolve it via
/// CardsExtensionCommandsProvider.GetCommandItem(id).
/// The user right-clicks a list → pins it → it appears in the global
/// "Pinned" section at the top of Command Palette (same as the host-level
/// pinned entries in the Command Palette home screen).
/// </summary>
internal sealed partial class ListsPage : DynamicListPage
{
    private readonly CardsService _cardsService;
    private readonly CardsBoard _board;
    private List<CardsList> _lists = [];
    private string _errorMessage = string.Empty;
    // 30s - same threshold and rationale as CardsPage.StaleThreshold: the first
    // GetItems() (lazy load, since the ctor no longer fetches) and revisits
    // after a pause both refresh, without reloading during normal browsing.
    private DateTime _lastLoadTime = DateTime.MinValue;
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(30);

    private static readonly IconInfo CardsIcon = IconHelpers.FromRelativePaths(@"Assets\StoreLogo.png", @"Assets\StoreLogo.png");

    public ListsPage(CardsService cardsService, CardsBoard board)
    {
        _cardsService = cardsService;
        _board = board;

        Icon = CardsIcon;
        Title = board.Name;
        Name = "Open";
        PlaceholderText = "Search lists…";
        ShowDetails = false;

        // Deliberately NO LoadListsInBackground() here: BoardListPage.GetItems
        // constructs one ListsPage per board on every render, so a ctor load
        // fired one API call per board per search keystroke. The stale-check in
        // GetItems() (reached only when the user actually navigates here)
        // triggers the first load and refreshes on revisit after 30s.
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        // Auto-sign-out may have fired while this page was open (revoked
        // refresh token) - show the signed-out state, not the raw API error.
        if (!AuthService.Instance.IsAuthenticated)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Trello is not connected",
                    Subtitle = "Access was revoked or expired - go back to Cards and sign in",
                    Icon = CardsIcon,
                },
            ];
        }

        if (!string.IsNullOrEmpty(_errorMessage))
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Failed to load lists",
                    Subtitle = _errorMessage,
                    Icon = CardsIcon,
                },
            ];
        }

        // Lazy load + stale refresh: _lastLoadTime starts at DateTime.MinValue,
        // so the FIRST GetItems() (user actually navigated here) triggers the
        // load; revisits after 30s pick up external changes.
        if (!IsLoading && DateTime.UtcNow - _lastLoadTime > StaleThreshold)
        {
            LoadListsInBackground();
        }

        if (_lists.Count == 0 && !IsLoading)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "No lists found",
                    Subtitle = "This board has no open lists",
                    Icon = new IconInfo("\uE8FD"),
                },
            ];
        }

        var search = SearchText?.Trim() ?? string.Empty;
        var items = new List<IListItem>();

        foreach (var list in _lists)
        {
            if (!string.IsNullOrEmpty(search) && !list.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            // Each CardsPage gets a stable Id so the Command Palette host's built-in
            // Pin mechanism can call GetCommandItem(id) to restore the pinned entry.
            // Id encodes list name and board name so GetCommandItem can display them
            // correctly. Names are URL-encoded so a name containing '|' cannot
            // corrupt the id|name|board split.
            // Format: "cards.list.{listId}|{listName}|{boardName}"
            var cardsPage = new CardsPage(_cardsService, list, _lists);
            cardsPage.Id = $"cards.list.{list.Id}|{CardsExtensionCommandsProvider.EncodeIdName(list.Name)}|{CardsExtensionCommandsProvider.EncodeIdName(_board.Name)}";

            // "Search cards" actions - appear in the More (Ctrl+K) menu.
            // Two separate pages: one for open cards, one for archived cards.
            // Both use GET /1/search?query=…&idBoards={boardId} and filter
            // client-side based on the card's "closed" field.
            var searchCardsPage = new SearchCardsPage(_cardsService, _board, searchArchived: false);
            var searchArchivedCardsPage = new SearchCardsPage(_cardsService, _board, searchArchived: true);

            items.Add(new ListItem(cardsPage)
            {
                Title = list.Name,
                Icon = new IconInfo("\uE8FD"), // BulletedList
                MoreCommands =
                [
                    new CommandContextItem(searchCardsPage)
                    {
                        Title = "Search cards on this board",
                        Icon = new IconInfo("\uE721"), // Search icon
                    },
                    new CommandContextItem(searchArchivedCardsPage)
                    {
                        Title = "Search archived cards on this board",
                        Icon = new IconInfo("\uE74D"), // Delete/Archive icon
                    },
                ],
            });
        }

        return [.. items];
    }

    private void LoadListsInBackground()
    {
        IsLoading = true;
        _errorMessage = string.Empty;

        Task.Run(async () =>
        {
            try
            {
                _lists = await _cardsService.GetListsAsync(_board.Id, CancellationToken.None).ConfigureAwait(false);
                _lastLoadTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _lists = [];
                _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                RaiseItemsChanged();
            }
        });
    }
}
