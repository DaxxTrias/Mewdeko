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
	[Table("TicketTags")]
	public class TicketTag
	{
		[Column("Id"         , IsPrimaryKey = true , IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id          { get; set; } // integer
		[Column("GuildId"                                                                                       )] public ulong   GuildId     { get; set; } // numeric(20,0)
		[Column("TagId"      , CanBeNull    = false                                                             )] public string    TagId       { get; set; } = null!; // text
		[Column("Name"       , CanBeNull    = false                                                             )] public string    Name        { get; set; } = null!; // text
		[Column("Description", CanBeNull    = false                                                             )] public string    Description { get; set; } = null!; // text
		[Column("Color"                                                                                         )] public long      Color       { get; set; } // bigint
		[Column("DateAdded"                                                                                     )] public DateTime? DateAdded   { get; set; } // timestamp (6) without time zone
	}
}
