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
	[Table("Giveaways")]
	public class Giveaway
	{
		[Column("Id"             , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id              { get; set; } // integer
		[Column("When"                                                                                             )] public DateTime  When            { get; set; } // timestamp (6) without time zone
		[Column("ChannelId"                                                                                        )] public ulong   ChannelId       { get; set; } // numeric(20,0)
		[Column("ServerId"                                                                                         )] public ulong   ServerId        { get; set; } // numeric(20,0)
		[Column("Ended"                                                                                            )] public int       Ended           { get; set; } // integer
		[Column("MessageId"                                                                                        )] public ulong   MessageId       { get; set; } // numeric(20,0)
		[Column("Winners"                                                                                          )] public int       Winners         { get; set; } // integer
		[Column("UserId"                                                                                           )] public ulong   UserId          { get; set; } // numeric(20,0)
		[Column("Item"                                                                                             )] public string?   Item            { get; set; } // text
		[Column("RestrictTo"                                                                                       )] public string?   RestrictTo      { get; set; } // text
		[Column("BlacklistUsers"                                                                                   )] public string?   BlacklistUsers  { get; set; } // text
		[Column("BlacklistRoles"                                                                                   )] public string?   BlacklistRoles  { get; set; } // text
		[Column("Emote"                                                                                            )] public string?   Emote           { get; set; } // text
		[Column("DateAdded"                                                                                        )] public DateTime? DateAdded       { get; set; } // timestamp (6) without time zone
		[Column("UseButton"                                                                                        )] public bool      UseButton       { get; set; } // boolean
		[Column("UseCaptcha"                                                                                       )] public bool      UseCaptcha      { get; set; } // boolean
		[Column("MessageCountReq"                                                                                  )] public ulong   MessageCountReq { get; set; } // numeric(20,0)
	}
}
