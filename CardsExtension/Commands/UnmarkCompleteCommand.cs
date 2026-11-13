using System;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Models;
using CardsExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Commands;

/// <summary>
/// Removes the "complete" mark from a card's due date.
/// Calls PUT /1/cards/{cardId}?dueComplete=false - symmetric to MarkCompleteCommand.
/// Shown in CardActionsPage only when the card's DueComplete is already true.
/// </summary>
internal sealed partial class UnmarkCompleteCommand : InvokableCommand
{
    private readonly CardsService _cardsService;
    private readonly CardsCard _card;
    private readonly Action? _onCompleted;

    public UnmarkCompleteCommand(CardsService cardsService, CardsCard card, Action? onCompleted = null)
    {
        _cardsService = cardsService;
        _card = card;
        _onCompleted = onCompleted;
        Name = "Mark as incomplete";
        Icon = new IconInfo("\uE739"); // Checkbox (unchecked)
    }

    public override CommandResult Invoke()
    {
        Task.Run(async () =>
        {
            try
            {
                await _cardsService.UnmarkCardCompleteAsync(_card.Id, CancellationToken.None).ConfigureAwait(false);
                // Update local state so the UI reflects the change without a full
                // API reload. This keeps the same CardsCard object → same ListItem
                // → same MoreCommands (with the toggle now showing "Mark as complete")
                // → SecondaryCommand stays initialized → Ctrl+Enter works.
                _card.DueComplete = false;
                _onCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                // Show a toast so the user knows the action failed (e.g. rate limit).
                var toast = new ToastStatusMessage($"Failed to unmark complete: {ex.Message}");
                toast.Show();
            }
        });

        return CommandResult.KeepOpen();
    }
}
