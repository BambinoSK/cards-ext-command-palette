using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CardsExtension.Models;
using CardsExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CardsExtension.Pages;

/// <summary>
/// ContentPage that hosts an Adaptive Card form for adding a comment to a card.
/// API: POST /1/cards/{cardId}/actions/comments?text={comment}
/// </summary>
internal sealed partial class AddCommentPage : ContentPage
{
    private readonly AddCommentForm _form;

    public AddCommentPage(CardsService cardsService, CardsCard card, Action? onCompleted = null)
    {
        _form = new AddCommentForm(cardsService, card, onCompleted);
        Icon = new IconInfo("\uE90A"); // Comment
        Title = "Add comment";
        Name = "Add comment";
    }

    public override IContent[] GetContent() => [_form];

    // ── Inner FormContent ────────────────────────────────────────────────────

    /// <summary>
    /// Adaptive Card form with a multiline text input for the comment body.
    /// On submit, POSTs the comment to API and navigates back.
    /// </summary>
    private sealed partial class AddCommentForm : FormContent
    {
        private readonly CardsService _cardsService;
        private readonly CardsCard _card;
        private readonly Action? _onCompleted;

        public AddCommentForm(CardsService cardsService, CardsCard card, Action? onCompleted)
        {
            _cardsService = cardsService;
            _card = card;
            _onCompleted = onCompleted;

            TemplateJson = """
            {
                "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                "type": "AdaptiveCard",
                "version": "1.6",
                "body": [
                    {
                        "type": "TextBlock",
                        "size": "medium",
                        "weight": "bolder",
                        "text": "Add comment to \"${title}\"",
                        "horizontalAlignment": "center",
                        "wrap": true,
                        "style": "heading"
                    },
                    {
                        "type": "Input.Text",
                        "label": "Comment",
                        "style": "text",
                        "id": "CommentText",
                        "isRequired": true,
                        "errorMessage": "Comment cannot be empty",
                        "placeholder": "Type your comment…",
                        "isMultiline": true
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "Add comment",
                        "data": {
                            "id": "AddComment"
                        }
                    }
                ]
            }
            """;

            DataJson = new JsonObject { ["title"] = card.Name }.ToJsonString();
        }

        public override CommandResult SubmitForm(string payload)
        {
            var formInput = JsonNode.Parse(payload)?.AsObject();
            var comment = formInput?["CommentText"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(comment))
                return CommandResult.KeepOpen();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _cardsService.AddCommentAsync(_card.Id, comment, CancellationToken.None).ConfigureAwait(false);
                    _onCompleted?.Invoke();
                }
                catch (Exception ex)
                {
                    var toast = new ToastStatusMessage($"Failed to add comment: {ex.Message}");
                    toast.Show();
                }
            });

            return CommandResult.GoBack();
        }
    }
}
