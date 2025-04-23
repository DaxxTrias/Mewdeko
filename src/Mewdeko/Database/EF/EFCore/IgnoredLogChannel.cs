using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a channel that is ignored for logging purposes.
/// </summary>
public class IgnoredLogChannel : DbEntity
{
    /// <summary>
    ///     Gets or sets the log setting ID.
    /// </summary>
    [ForeignKey("LogSettingId")]
    public int LogSettingId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }
}