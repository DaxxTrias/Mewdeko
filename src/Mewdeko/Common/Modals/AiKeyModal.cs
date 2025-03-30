using Discord.Interactions;
// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common.Modals;

/// <summary>
/// Modal for entering key for ai
/// </summary>
public class AiKeyModal : IModal
{
    /// <summary>
    /// Modal Title
    /// </summary>
    public string Title => "Set AI API Key";

    /// <summary>
    /// The api key for ai
    /// </summary>
    [InputLabel("API Key")]
    [ModalTextInput("ai_key", TextInputStyle.Short, "Enter your API key here")]
    [RequiredInput]
    public string ApiKey { get; set; }
}