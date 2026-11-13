using System;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Models;
using CardsExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Commands;

/// <summary>
/// Archives a card via PUT /1/cards/{cardId}?closed=true.
/// </summary>
internal sealed partial class ArchiveCardCommand : InvokableCommand
{
    private readonly CardsService _cardsService;
    private readonly CardsCard _card;
    private readonly Action? _onCompleted;

    public ArchiveCardCommand(CardsService cardsService, CardsCard card, Action? onCompleted = null)
    {
        _cardsService = cardsService;
        _card = card;
        _onCompleted = onCompleted;
        Name = "Archive card";
        Icon = new IconInfo("\uE74D"); // Delete/Archive icon
    }

    public override CommandResult Invoke()
    {
        Task.Run(async () =>
        {
            try
            {
                await _cardsService.ArchiveCardAsync(_card.Id, CancellationToken.None).ConfigureAwait(false);
                _onCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                // Show a toast so the user knows the archive failed (e.g. rate limit).
                var toast = new ToastStatusMessage($"Failed to archive card: {ex.Message}");
                toast.Show();
            }
        });

        return CommandResult.KeepOpen();
    }
}
