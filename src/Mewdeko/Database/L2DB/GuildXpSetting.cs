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
	[Table("GuildXpSettings")]
	public class GuildXpSetting
	{
		[Column("Id"                       , IsPrimaryKey = true , IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id                        { get; set; } // integer
		[Column("GuildId"                                                                                                     )] public ulong   GuildId                   { get; set; } // numeric(20,0)
		[Column("XpMultiplier"                                                                                                )] public double    XpMultiplier              { get; set; } // double precision
		[Column("XpPerMessage"                                                                                                )] public int       XpPerMessage              { get; set; } // integer
		[Column("MessageXpCooldown"                                                                                           )] public int       MessageXpCooldown         { get; set; } // integer
		[Column("VoiceXpPerMinute"                                                                                            )] public int       VoiceXpPerMinute          { get; set; } // integer
		[Column("VoiceXpTimeout"                                                                                              )] public int       VoiceXpTimeout            { get; set; } // integer
		[Column("FirstMessageBonus"                                                                                           )] public int       FirstMessageBonus         { get; set; } // integer
		[Column("XpCurveType"                                                                                                 )] public int       XpCurveType               { get; set; } // integer
		[Column("XpGainDisabled"                                                                                              )] public bool      XpGainDisabled            { get; set; } // boolean
		[Column("CustomXpImageUrl"         , CanBeNull    = false                                                             )] public string    CustomXpImageUrl          { get; set; } = null!; // text
		[Column("LevelUpMessage"           , CanBeNull    = false                                                             )] public string    LevelUpMessage            { get; set; } = null!; // text
		[Column("ExclusiveRoleRewards"                                                                                        )] public bool      ExclusiveRoleRewards      { get; set; } // boolean
		[Column("EnableXpDecay"                                                                                               )] public bool      EnableXpDecay             { get; set; } // boolean
		[Column("InactivityDaysBeforeDecay"                                                                                   )] public int       InactivityDaysBeforeDecay { get; set; } // integer
		[Column("DailyDecayPercentage"                                                                                        )] public double    DailyDecayPercentage      { get; set; } // double precision
		[Column("DateAdded"                                                                                                   )] public DateTime? DateAdded                 { get; set; } // timestamp (6) without time zone
	}
}
