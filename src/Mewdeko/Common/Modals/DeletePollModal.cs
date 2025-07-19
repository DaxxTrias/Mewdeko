using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Modal for confirming poll deletion.
/// </summary>
public class DeletePollModal : IModal
{
    /// <summary>
    /// Gets or sets the confirmation text for deleting the poll.
    /// </summary>
    [InputLabel("Type 'DELETE' to confirm")]
    [ModalTextInput("confirmation", TextInputStyle.Short, "DELETE", 6, 6)]
    public string Confirmation { get; set; } = string.Empty;

    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Delete Poll";
}