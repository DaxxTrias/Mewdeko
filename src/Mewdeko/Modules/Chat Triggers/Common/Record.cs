namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Represents an error that occurred during interaction with chat triggers.
/// </summary>
/// <param name="ErrorKey">The errorkey string.</param>
/// <param name="CtIds">The CtIds parameter.</param>
/// <param name="CtRealNames">The ctrealnames string.</param>
public record ChatTriggersInteractionError(string ErrorKey, int[] CtIds, string[] CtRealNames);