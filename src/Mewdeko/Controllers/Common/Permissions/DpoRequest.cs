namespace Mewdeko.Controllers.Common.Permissions;

/// <summary>
///     Request model for Discord permission overrides
/// </summary>
public class DpoRequest
{
    /// <summary>
    ///     The command name to apply permissions to
    /// </summary>
    public string Command { get; set; }

    /// <summary>
    ///     The Discord permissions value as a ulong
    /// </summary>
    public ulong Permissions { get; set; }
}