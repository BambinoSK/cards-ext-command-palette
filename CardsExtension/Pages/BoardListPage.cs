using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Auth;
using CardsExtension.Commands;
using CardsExtension.Models;
using CardsExtension.Services;
using CardsExtension.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Pages;

/// <summary>
/// Top-level page: checks auth state first.
/// - Not authenticated → shows a single-click "Connect to Trello" item directly
///   (OAuth 2.0 browser sign-in starts immediately - no nested auth page), or a
///   "waiting" status row while a sign-in is completing in the browser.
/// - Authenticated → lists all open boards; each drills to ListsPage.
/// - "Disconnect Trello" item always available at the bottom when authenticated.
/// </summary>
internal sealed partial class BoardListPage : DynamicListPage, IDisposable
{
    private readonly CardsService _cardsService;
    private List<CardsBoard> _boards = [];
    private string _errorMessage = string.Empty;
    // When the boards were last loaded (or a load was last attempted).
    private DateTime _lastLoadTime = DateTime.MinValue;
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(60);

    // logo PNG asset
    private static readonly IconInfo CardsIcon =
        IconHelpers.FromRelativePaths(@"Assets\StoreLogo.png", @"Assets\StoreLogo.png");

    public BoardListPage(CardsService cardsService)
    {
        _cardsService = cardsService;

        Icon = CardsIcon;
        Title = ExtensionIdentity.DisplayName;
        Name = "Open";
        PlaceholderText = "Search boards…";
        ShowDetails = false;

        // Start loading boards only if already authenticated.
        if (AuthService.Instance.IsAuthenticated)
        {
            LoadBoardsInBackground();
        }

        // Auto-sign-out (revoked refresh token → invalid_grant, or explicit
        // Disconnect from another page instance) must flip this page back to
        // the Connect row even while it is visible.
        AuthService.SignedOut += OnSignedOut;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged();
    }

    /// <summary>
    /// Disposes the page: unsubscribes from AuthService.SignedOut. The
    /// subscription held this page alive for process lifetime (BoardListPage
    /// is constructed on every provider creation and pinned-board restore),
    /// so removing it lets the GC reclaim the page when the host drops it.
    /// </summary>
    public void Dispose()
    {
        AuthService.SignedOut -= OnSignedOut;
        GC.SuppressFinalize(this);
    }

    public override IListItem[] GetItems()
    {
        // ── Not authenticated ────────────────────────────────────────────────
        if (!AuthService.Instance.IsAuthenticated)
        {
            // Sign-in already running (browser open) → status row + cancel.
            // The palette stays on THIS page; when the background handshake
            // completes (or fails / times out / is cancelled), OnAuthenticated /
            // OnSignInFailed raise ItemsChanged and this page re-renders.
            if (AuthService.Instance.PendingRequest is not null)
            {
                return
                [
                    new ListItem(new NoOpCommand())
                    {
                        Title = "Waiting for you to finish signing in",
                        Subtitle = "Complete the login and click Allow in your browser",
                        Icon = new IconInfo("\uE895"), // Sync / progress
                    },
                    new ListItem(new CancelSignInCommand(OnSignInFailed))
                    {
                        Title = "Cancel sign-in",
                        Subtitle = "Aborts the pending browser sign-in (e.g. you closed the tab)",
                        Icon = new IconInfo("\uE711"), // Cancel
                    },
                ];
            }

            // Not signed in → single-click connect right here (no nested page).
            return
            [
                new ListItem(new ConnectCardsCommand(OnAuthenticated, OnSignInStarted, OnSignInFailed))
                {
                    Title = "Connect to Trello",
                    Subtitle = "Opens your browser - log in and click Allow; the extension finishes the rest automatically",
                    Icon = new IconInfo("\uE774"), // Globe
                },
            ];
        }

        // ── Stale refresh ────────────────────────────────────────────────────
        // IsLoading is set synchronously, so this also prevents stacking loads.
        if (!IsLoading && DateTime.UtcNow - _lastLoadTime > StaleThreshold)
        {
            LoadBoardsInBackground();
        }

        // ── API error ────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_errorMessage))
        {
            return
            [
                new ListItem(new AnonymousCommand(RetryLoad) { Name = "Retry", Result = CommandResult.KeepOpen() })
                {
                    Title = "Failed to load boards",
                    Subtitle = _errorMessage,
                    Icon = new IconInfo("\uE783"),
                },
                DisconnectItem(),
            ];
        }

        if (_boards.Count == 0 && !IsLoading)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "No boards found",
                    Subtitle = "No open boards visible to your Trello account",
                    Icon = CardsIcon,
                },
                DisconnectItem(),
            ];
        }

        var search = SearchText?.Trim() ?? string.Empty;
        var items = new List<IListItem>();

        foreach (var board in _boards)
        {
            if (!string.IsNullOrEmpty(search) && !board.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var listsPage = new ListsPage(_cardsService, board);
            // Stable Id so Command Palette's built-in Pin mechanism can restore
            // a pinned board via GetCommandItem(id) after restart. Same pattern
            // as ListsPage sets on CardsPage (cards.list.{…}).
            // Format: "cards.board.{boardId}|{boardName}" (name URL-encoded -
            // see CardsExtensionCommandsProvider.EncodeIdName).
            listsPage.Id = $"cards.board.{board.Id}|{CardsExtensionCommandsProvider.EncodeIdName(board.Name)}";

            items.Add(new ListItem(listsPage)
            {
                Title = board.Name,
                Icon = CardsIcon,
            });
        }

        // Settings + Disconnect always at the bottom when authenticated.
        items.Add(new Separator());
        items.Add(SettingsItem());
        items.Add(DisconnectItem());

        return [.. items];
    }

    private static ListItem SettingsItem() =>
        new ListItem(CardsSettingsManager.Instance.Settings.SettingsPage)
        {
            Title = "Settings",
            Subtitle = "New card position, auto-show details",
            Icon = new IconInfo("\uE713"), // Settings gear
        };

    private ListItem DisconnectItem() =>
        new ListItem(new DisconnectCardsCommand(OnSignedOut))
        {
            Title = "Disconnect Trello",
            Subtitle = "Remove stored sign-in",
            Icon = new IconInfo("\uE7E8"), // LeaveChat / sign-out
        };

    /// <summary>
    /// Called by ConnectCardsCommand right after the browser opens - swaps the
    /// Connect row for the "waiting" status row while the user is in the browser.
    /// </summary>
    private void OnSignInStarted()
    {
        RaiseItemsChanged();
    }

    /// <summary>
    /// Called when a sign-in attempt ends without success (failure, 5-minute
    /// timeout, or user cancellation) - the waiting rows flip back to the
    /// Connect row so the user is never stuck.
    /// </summary>
    private void OnSignInFailed()
    {
        RaiseItemsChanged();
    }

    /// <summary>Called by ConnectCardsCommand after a successful OAuth 2.0 sign-in.</summary>
    private void OnAuthenticated()
    {
        LoadBoardsInBackground();
        RaiseItemsChanged();
    }

    /// <summary>Called by DisconnectCardsCommand after signing out.</summary>
    private void OnSignedOut()
    {
        _boards = [];
        _errorMessage = string.Empty;
        // Forces a reload on the next render after signing back in.
        _lastLoadTime = DateTime.MinValue;
        RaiseItemsChanged();
    }

    private void RetryLoad()
    {
        if (!IsLoading)
        {
            LoadBoardsInBackground();
        }
    }

    private void LoadBoardsInBackground()
    {
        IsLoading = true;
        // _errorMessage is cleared on success only, so the error row stays while a retry runs.

        Task.Run(async () =>
        {
            try
            {
                var all = await _cardsService.GetBoardsAsync(CancellationToken.None).ConfigureAwait(false);
                var open = new List<CardsBoard>();
                foreach (var b in all)
                {
                    if (!b.Closed) open.Add(b);
                }
                open.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
                _boards = open;
                _errorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                // A failed refresh keeps the boards already shown; only an empty page reports the error.
                if (_boards.Count == 0)
                {
                    _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
                }
            }
            finally
            {
                // Also set on failure: the stale check retries only after StaleThreshold.
                _lastLoadTime = DateTime.UtcNow;
                IsLoading = false;
                RaiseItemsChanged();
            }
        });
    }
}
