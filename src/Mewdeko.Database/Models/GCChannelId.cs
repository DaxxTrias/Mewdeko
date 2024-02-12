namespace Mewdeko.Database.Models;

// ReSharper disable once InconsistentNaming
public class GcChannelId : DbEntity
{
    public GuildConfig GuildConfig { get; set; }
    public ulong ChannelId { get; set; }

    public override bool Equals(object obj) => obj is GcChannelId gc && gc.ChannelId == ChannelId;

    public override int GetHashCode() => ChannelId.GetHashCode();
}