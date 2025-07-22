namespace Mewdeko.Controllers.Common.Chat;

/// <summary>
///     Request model for updating chat log names.
/// </summary>
public class UpdateChatLogNameRequest
{
    /// <summary>
    ///     The new name for the chat log.
    /// </summary>
    public string Name { get; set; }
}