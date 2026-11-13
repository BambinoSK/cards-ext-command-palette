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
/// Moves a card to a target list via PUT /1/cards/{cardId}?idList={newListId}.
/// </summary>
internal sealed partial class MoveCardCommand : InvokableCommand
{
    private readonly CardsService _cardsService;
    private readonly CardsCard _card;
    private readonly CardsList _targetList;
    private readonly Action? _onCompleted;

    public MoveCardCommand(CardsService cardsService, CardsCard card, CardsList targetList, Action? onCompleted = null)
    {
        _cardsService = cardsService;
        _card = card;
        _targetList = targetList;
        _onCompleted = onCompleted;
        Name = $"Move to \"{targetList.Name}\"";
        Icon = new IconInfo("\uE8AB"); // MoveToFolder icon
    }

    public override CommandResult Invoke()
    {
        var pos = CardsSettingsManager.Instance.NewCardPosition;

        Task.Run(async () =>
        {
            try
            {
                await _cardsService.MoveCardAsync(_card.Id, _targetList.Id, pos, CancellationToken.None).ConfigureAwait(false);
                _onCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                var toast = new ToastStatusMessage($"Failed to move card: {ex.Message}");
                toast.Show();
            }
        });

        return CommandResult.GoBack();
    }
}
