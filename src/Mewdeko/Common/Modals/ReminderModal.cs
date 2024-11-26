using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for setting a reminder.
/// </summary>
public class ReminderModal : IModal
{
    /// <summary>
    ///     Gets or sets the reminder text.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Reminder",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Enter your
    ///     reminder.".
    /// </remarks>
    [InputLabel("Reminder")]
    [ModalTextInput("reminder", TextInputStyle.Paragraph, "Enter your reminder.")]
    public string? Reminder { get; set; }

    /// <summary>
    ///     Gets or sets the target user for the reminder.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Target User",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Short and a placeholder of "Enter the target user.".
    /// </remarks>
    [InputLabel("Target User")]
    [ModalTextInput("target_user", TextInputStyle.Short, "Enter the target user.")]
    public string? TargetUser { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            //return "New Reminder";
            return TargetUser != null ? $"New Reminder for {TargetUser}" : "New Reminder";
        }
    }
}