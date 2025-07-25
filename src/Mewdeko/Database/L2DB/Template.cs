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
	[Table("Template")]
	public class Template
	{
		[Column("Id"                 , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id                  { get; set; } // integer
		[Column("GuildId"                                                                                              )] public ulong   GuildId             { get; set; } // numeric(20,0)
		[Column("OutputSizeX"                                                                                          )] public int       OutputSizeX         { get; set; } // integer
		[Column("OutputSizeY"                                                                                          )] public int       OutputSizeY         { get; set; } // integer
		[Column("TimeOnLevelFormat"                                                                                    )] public string?   TimeOnLevelFormat   { get; set; } // text
		[Column("TimeOnLevelX"                                                                                         )] public int       TimeOnLevelX        { get; set; } // integer
		[Column("TimeOnLevelY"                                                                                         )] public int       TimeOnLevelY        { get; set; } // integer
		[Column("TimeOnLevelFontSize"                                                                                  )] public int       TimeOnLevelFontSize { get; set; } // integer
		[Column("TimeOnLevelColor"                                                                                     )] public string?   TimeOnLevelColor    { get; set; } // text
		[Column("ShowTimeOnLevel"                                                                                      )] public bool      ShowTimeOnLevel     { get; set; } // boolean
		[Column("AwardedX"                                                                                             )] public int       AwardedX            { get; set; } // integer
		[Column("AwardedY"                                                                                             )] public int       AwardedY            { get; set; } // integer
		[Column("AwardedFontSize"                                                                                      )] public int       AwardedFontSize     { get; set; } // integer
		[Column("AwardedColor"                                                                                         )] public string?   AwardedColor        { get; set; } // text
		[Column("ShowAwarded"                                                                                          )] public bool      ShowAwarded         { get; set; } // boolean
		[Column("TemplateUserId"                                                                                       )] public int       TemplateUserId      { get; set; } // integer
		[Column("TemplateGuildId"                                                                                      )] public int       TemplateGuildId     { get; set; } // integer
		[Column("TemplateClubId"                                                                                       )] public int       TemplateClubId      { get; set; } // integer
		[Column("TemplateBarId"                                                                                        )] public int       TemplateBarId       { get; set; } // integer
		[Column("DateAdded"                                                                                            )] public DateTime? DateAdded           { get; set; } // timestamp (6) without time zone

		#region Associations
		/// <summary>
		/// FK_Template_TemplateBar_TemplateBarId
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(TemplateBarId), OtherKey = nameof(DataModel.TemplateBar.Id))]
		public TemplateBar TemplateBar { get; set; } = null!;

		/// <summary>
		/// FK_Template_TemplateClub_TemplateClubId
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(TemplateClubId), OtherKey = nameof(DataModel.TemplateClub.Id))]
		public TemplateClub TemplateClub { get; set; } = null!;

		/// <summary>
		/// FK_Template_TemplateGuild_TemplateGuildId
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(TemplateGuildId), OtherKey = nameof(DataModel.TemplateGuild.Id))]
		public TemplateGuild TemplateGuild { get; set; } = null!;

		/// <summary>
		/// FK_Template_TemplateUser_TemplateUserId
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(TemplateUserId), OtherKey = nameof(DataModel.TemplateUser.Id))]
		public TemplateUser TemplateUser { get; set; } = null!;
		#endregion
	}
}
