// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
	[Table("GuildAiConfig")]
	public class GuildAiConfig
	{
		[Column("Id"          , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id           { get; set; } // integer
		[Column("GuildId"                                                                                       )] public ulong   GuildId      { get; set; } // numeric(20,0)
		[Column("Enabled"                                                                                       )] public bool      Enabled      { get; set; } // boolean
		[Column("ApiKey"                                                                                        )] public string?   ApiKey       { get; set; } // text
		[Column("Provider"                                                                                      )] public int       Provider     { get; set; } // integer
		[Column("Model"                                                                                         )] public string?   Model        { get; set; } // text
		[Column("ChannelId"                                                                                     )] public ulong   ChannelId    { get; set; } // numeric(20,0)
		[Column("SystemPrompt"                                                                                  )] public string?   SystemPrompt { get; set; } // text
		[Column("TokensUsed"                                                                                    )] public int       TokensUsed   { get; set; } // integer
		[Column("DateAdded"                                                                                     )] public DateTime? DateAdded    { get; set; } // timestamp (6) without time zone
		[Column("CustomEmbed"                                                                                   )] public string?   CustomEmbed  { get; set; } // text
		[Column("WebhookUrl"                                                                                    )] public string?   WebhookUrl   { get; set; } // text
	}
}
