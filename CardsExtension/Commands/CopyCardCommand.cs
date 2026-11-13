using System;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Models;
using CardsExtension.Services;
using CardsExtension.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Commands;

/// <summary>
/// Copies a card to a target list via POST /1/cards?idCardSource={sourceId}&idList={targetListId}&keepFromSource=all.
/// Unlike MoveCardCommand, copying to the card's current list is valid (creates a duplicate).
/// </summary>
internal sealed partial class CopyCardCommand : InvokableCommand
{
    private readonly CardsService _cardsService;
    private readonly CardsCard _card;
    private readonly CardsList _targetList;
    private readonly Action? _onCompleted;

    public CopyCardCommand(CardsService cardsService, CardsCard card, CardsList targetList, Action? onCompleted = null)
    {
        _cardsService = cardsService;
        _card = card;
        _targetList = targetList;
        _onCompleted = onCompleted;
        Name = $"Copy to \"{targetList.Name}\"";
        Icon = new IconInfo("\uE8C8"); // Copy icon
    }

    public override CommandResult Invoke()
    {
        var pos = CardsSettingsManager.Instance.NewCardPosition;

        Task.Run(async () =>
        {
            try
            {
                await _cardsService.CopyCardAsync(_card.Id, _targetList.Id, pos, CancellationToken.None).ConfigureAwait(false);
                _onCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                var toast = new ToastStatusMessage($"Failed to copy card: {ex.Message}");
                toast.Show();
            }
        });

        return CommandResult.GoBack();
    }
}
