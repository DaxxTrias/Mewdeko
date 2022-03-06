﻿namespace Mewdeko.Database.Models;

public class MultiGreet : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Message { get; set; } = "Welcome %user%";
    public bool GreetBots { get; set; } = false;
    public ulong DeleteTime { get; set; } = 1;
    public string WebhookUrl { get; set; }
}