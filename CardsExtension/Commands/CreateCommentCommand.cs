using System;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Commands;

/// <summary>
/// Posts a new comment with pre-supplied text to a card.
/// Invoked from CardCommentsPage "Post comment" quick-add item.
/// After posting, the optional onCompleted callback reloads the comments list.
/// API: POST /1/cards/{cardId}/actions/comments?text={text}
/// </summary>
internal sealed partial class CreateCommentCommand : InvokableCommand
{
    private readonly CardsService _cardsService;
    private readonly string _cardId;
    private readonly string _commentText;
    private readonly Action? _onCompleted;

    public CreateCommentCommand(CardsService cardsService, string cardId, string commentText, Action? onCompleted = null)
    {
        _cardsService = cardsService;
        _cardId = cardId;
        _commentText = commentText;
        _onCompleted = onCompleted;
        Name = "Post comment";
        Icon = new IconInfo("\uE710"); // Add icon
    }

    public override CommandResult Invoke()
    {
        Task.Run(async () =>
        {
            try
            {
                await _cardsService.AddCommentAsync(_cardId, _commentText, CancellationToken.None).ConfigureAwait(false);
                _onCompleted?.Invoke();

                var toast = new ToastStatusMessage(new StatusMessage { Message = "Comment added", State = MessageState.Success });
                toast.Show();
            }
            catch (Exception ex)
            {
                var toast = new ToastStatusMessage($"Failed to add comment: {ex.Message}");
                toast.Show();
            }
        });

        return CommandResult.KeepOpen();
    }
}
