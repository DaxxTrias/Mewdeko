namespace Mewdeko.Modules.Games.Common;

/// <summary>
///     Specifies the type of poll.
/// </summary>
public enum PollType
{
    /// <summary>
    ///     Poll with a single answer option.
    /// </summary>
    SingleAnswer,

    /// <summary>
    ///     Poll that allows changing the answer.
    /// </summary>
    AllowChange,

    /// <summary>
    ///     Poll with multiple answer options.
    /// </summary>
    MultiAnswer,

    /// <summary>
    ///     Poll that has ended.
    /// </summary>
    PollEnded
}