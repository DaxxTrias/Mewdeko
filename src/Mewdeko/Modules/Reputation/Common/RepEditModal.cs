using Discord.Interactions;

namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Modal for editing reputation configuration values.
/// </summary>
public class RepEditModal : IModal
{
    /// <summary>
    ///     Gets or sets the input value from the modal.
    /// </summary>
    [ModalTextInput("rep_input")]
    public string Value { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Edit Configuration";
        }
    }
}