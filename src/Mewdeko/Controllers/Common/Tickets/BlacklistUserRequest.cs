namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for blacklisting a user
/// </summary>
public class BlacklistUserRequest
{
    /// <summary>
    ///     The optional reason for the blacklist
    /// </summary>
    public string? Reason { get; set; }
}