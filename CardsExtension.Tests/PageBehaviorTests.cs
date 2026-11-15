// Behavior of pages and commands that can be driven without the Command
// Palette host: the test plays the host by calling GetItems() itself, and the
// REST API is the StubCardsHandler fake.

using CardsExtension.Commands;
using CardsExtension.Models;
using CardsExtension.Pages;
using CardsExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Xunit;

namespace CardsExtension.Tests;

public sealed class PageBehaviorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static (CardsService Service, StubCardsHandler Handler) MakeService()
    {
        var handler = new StubCardsHandler();
        return (new CardsService(handler, _ => Task.FromResult("test-access-token")), handler);
    }

    // ── Move up / down ───────────────────────────────────────────────────────

    private static CardsCard Card(string id, double pos) => new() { Id = id, Pos = pos };

    // The page replaces its card list on reload; the command must use the list as it is when invoked.
    [Fact]
    public async Task MoveUp_PositionsAgainstTheListAsItIsWhenInvoked()
    {
        var (service, handler) = MakeService();
        handler.Stub("PUT", "/1/cards/b", (200, """{ "id": "b", "pos": 75 }"""));

        var b = Card("b", 200);
        IReadOnlyList<CardsCard> current = [Card("a", 100), b, Card("c", 300)];
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new MoveCardUpCommand(service, b, () => current, () => done.TrySetResult());

        // The page reloads: a new card X now sits at the top.
        current = [Card("x", 50), Card("a", 100), Card("b", 200), Card("c", 300)];

        command.Invoke();
        await done.Task.WaitAsync(Timeout);

        // One up from 3rd place = between X (50) and A (100).
        Assert.EndsWith("/1/cards/b?pos=75", Assert.Single(handler.Requests).Url);
        Assert.Equal(75, b.Pos);
    }

    [Fact]
    public async Task MoveDown_PositionsAgainstTheListAsItIsWhenInvoked()
    {
        var (service, handler) = MakeService();
        handler.Stub("PUT", "/1/cards/b", (200, """{ "id": "b", "pos": 350 }"""));

        var b = Card("b", 200);
        IReadOnlyList<CardsCard> current = [Card("a", 100), b, Card("c", 300)];
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new MoveCardDownCommand(service, b, () => current, () => done.TrySetResult());

        // The page reloads: a new card Y now sits at the bottom.
        current = [Card("a", 100), Card("b", 200), Card("c", 300), Card("y", 400)];

        command.Invoke();
        await done.Task.WaitAsync(Timeout);

        // One down from 2nd place = between C (300) and Y (400).
        Assert.EndsWith("/1/cards/b?pos=350", Assert.Single(handler.Requests).Url);
    }

    // ── Failed loads ─────────────────────────────────────────────────────────

    // A failed load re-renders the page; that render must show the error, not start another load.
    [Fact]
    public async Task CommentsPage_FailedLoad_ShowsTheErrorAndDoesNotReloadOnEveryRender()
    {
        var (service, handler) = MakeService(); // nothing stubbed: every request is a 404
        var page = new CardCommentsPage(service, new CardsCard { Id = "c1", Name = "Card" });
        var errorShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Simulated host: fetch the items again whenever the page says they changed.
        page.ItemsChanged += (_, _) =>
        {
            if (CommentLoads(handler) >= 20)
            {
                return; // Safety cap so a regression fails the assert instead of spinning.
            }

            if (page.GetItems().Any(item => item.Title == "Failed to load comments"))
            {
                errorShown.TrySetResult();
            }
        };

        page.GetItems();
        await errorShown.Task.WaitAsync(Timeout);
        await Task.Delay(500); // Room for a reload loop to show itself.

        Assert.Equal(1, CommentLoads(handler));
    }

    [Fact]
    public async Task CommentsPage_ErrorRow_RetriesOnEnter()
    {
        var (service, handler) = MakeService();
        var page = new CardCommentsPage(service, new CardsCard { Id = "c1", Name = "Card" });
        var rendered = new SemaphoreSlim(0);
        page.ItemsChanged += (_, _) => rendered.Release();

        page.GetItems();
        FirstRow errorRow;
        do
        {
            Assert.True(await rendered.WaitAsync(Timeout));
            errorRow = Snapshot(page);
        }
        while (errorRow.Title != "Failed to load comments");

        // The API recovers; Enter on the error row loads again.
        handler.Stub("GET", "/1/cards/c1/actions", (200, "[]"));
        errorRow.Invoke();

        FirstRow row;
        do
        {
            Assert.True(await rendered.WaitAsync(Timeout));
            row = Snapshot(page);
        }
        while (row.Title == "Failed to load comments");

        Assert.Equal("No comments", row.Title);
        Assert.Equal(2, CommentLoads(handler));
    }

    private static int CommentLoads(StubCardsHandler handler)
    {
        lock (handler.Requests)
        {
            return handler.Requests.Count(r => r.Url.Contains("/1/cards/c1/actions", StringComparison.Ordinal));
        }
    }

    /// <summary>First row of a page plus a way to press Enter on it.</summary>
    private sealed record FirstRow(string Title, Action Invoke);

    private static FirstRow Snapshot(CardCommentsPage page)
    {
        var items = page.GetItems();
        if (items.Length == 0)
        {
            return new FirstRow(string.Empty, () => { });
        }

        var first = items[0];
        return new FirstRow(first.Title, () => ((IInvokableCommand)first.Command).Invoke(null));
    }

    // ── Move to… / Copy to… search box ───────────────────────────────────────

    private static readonly IReadOnlyList<CardsList> BoardLists =
    [
        new() { Id = "l1", Name = "Backlog" },
        new() { Id = "l2", Name = "In progress" },
        new() { Id = "l3", Name = "Done" },
    ];

    private static string Titles(IListPage page) => string.Join(" | ", page.GetItems().Select(i => i.Title));

    private static readonly CardsCard CardInBacklog = new() { Id = "c1", Name = "Card", IdList = "l1", IdBoard = "b1" };

    [Fact]
    public void MoveCardPage_FiltersTheListsBySearchText()
    {
        var (service, _) = MakeService();
        var page = new MoveCardPage(CardInBacklog, service, BoardLists, () => { });

        Assert.Equal("In progress | Done", Titles(page));

        page.SearchText = "PROG";
        Assert.Equal("In progress", Titles(page));

        page.SearchText = "zzz";
        Assert.Equal("No matching lists", Titles(page));
    }

    [Fact]
    public void CopyCardPage_FiltersTheListsBySearchText()
    {
        var (service, _) = MakeService();
        var page = new CopyCardPage(CardInBacklog, service, BoardLists, () => { });

        Assert.Equal("Backlog | In progress | Done", Titles(page));

        page.SearchText = "back";
        Assert.Equal("Backlog", Titles(page));

        page.SearchText = "zzz";
        Assert.Equal("No matching lists", Titles(page));
    }
}
