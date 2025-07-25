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
	[Table("MusicPlaylistTracks")]
	public class MusicPlaylistTrack
	{
		[Column("Id"        , IsPrimaryKey = true , IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int      Id         { get; set; } // integer
		[Column("PlaylistId"                                                                                   )] public int      PlaylistId { get; set; } // integer
		[Column("Title"     , CanBeNull    = false                                                             )] public string   Title      { get; set; } = null!; // character varying(200)
		[Column("Uri"       , CanBeNull    = false                                                             )] public string   Uri        { get; set; } = null!; // character varying(500)
		[Column("Duration"                                                                                     )] public TimeSpan Duration   { get; set; } // interval
		[Column("Index"                                                                                        )] public int      Index      { get; set; } // integer
		[Column("DateAdded"                                                                                    )] public DateTime DateAdded  { get; set; } // timestamp (6) without time zone

		#region Associations
		/// <summary>
		/// FK_MusicPlaylistTracks_MusicPlaylists_PlaylistId
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(PlaylistId), OtherKey = nameof(MusicPlaylist.Id))]
		public MusicPlaylist Playlist { get; set; } = null!;
		#endregion
	}
}
