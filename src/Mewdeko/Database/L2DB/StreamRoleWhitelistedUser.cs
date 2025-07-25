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
	[Table("StreamRoleWhitelistedUser")]
	public class StreamRoleWhitelistedUser
	{
		[Column("Id"                  , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id                   { get; set; } // integer
		[Column("StreamRoleSettingsId"                                                                                  )] public int       StreamRoleSettingsId { get; set; } // integer
		[Column("UserId"                                                                                                )] public ulong   UserId               { get; set; } // numeric(20,0)
		[Column("Username"                                                                                              )] public string?   Username             { get; set; } // text
		[Column("DateAdded"                                                                                             )] public DateTime? DateAdded            { get; set; } // timestamp (6) without time zone

		#region Associations
		/// <summary>
		/// FK_StreamRoleWhitelistedUser_StreamRoleSettings_StreamRoleSett~
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(StreamRoleSettingsId), OtherKey = nameof(StreamRoleSetting.Id))]
		public StreamRoleSetting StreamRoleSettings { get; set; } = null!;
		#endregion
	}
}
