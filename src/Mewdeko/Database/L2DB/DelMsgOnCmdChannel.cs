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
	[Table("DelMsgOnCmdChannels")]
	public class DelMsgOnCmdChannel
	{
		[Column("Id"       , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id        { get; set; } // integer
		[Column("ChannelId"                                                                                  )] public ulong   ChannelId { get; set; } // numeric(20,0)
		[Column("State"                                                                                      )] public bool      State     { get; set; } // boolean
		[Column("DateAdded"                                                                                  )] public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
		[Column("GuildId"                                                                                    )] public ulong?  GuildId   { get; set; } // numeric(20,0)
	}
}
