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
	[Table("GuildConfigs")]
	public class GuildConfig
	{
		[Column("Id"                                , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id                                 { get; set; } // integer
		[Column("GuildId"                                                                                                             )] public ulong   GuildId                            { get; set; } // numeric(20,0)
		[Column("Prefix"                                                                                                              )] public string?   Prefix                             { get; set; } // text
		[Column("StaffRole"                                                                                                           )] public ulong   StaffRole                          { get; set; } // numeric(20,0)
		[Column("GameMasterRole"                                                                                                      )] public ulong   GameMasterRole                     { get; set; } // numeric(20,0)
		[Column("CommandLogChannel"                                                                                                   )] public ulong   CommandLogChannel                  { get; set; } // numeric(20,0)
		[Column("DeleteMessageOnCommand"                                                                                              )] public bool      DeleteMessageOnCommand             { get; set; } // boolean
		[Column("WarnMessage"                                                                                                         )] public string?   WarnMessage                        { get; set; } // text
		[Column("AutoAssignRoleId"                                                                                                    )] public string?   AutoAssignRoleId                   { get; set; } // text
		[Column("XpImgUrl"                                                                                                            )] public string?   XpImgUrl                           { get; set; } // text
		[Column("StatsOptOut"                                                                                                         )] public bool      StatsOptOut                        { get; set; } // boolean
		[Column("CurrencyName"                                                                                                        )] public string?   CurrencyName                       { get; set; } // text
		[Column("CurrencyEmoji"                                                                                                       )] public string?   CurrencyEmoji                      { get; set; } // text
		[Column("RewardAmount"                                                                                                        )] public int       RewardAmount                       { get; set; } // integer
		[Column("RewardTimeoutSeconds"                                                                                                )] public int       RewardTimeoutSeconds               { get; set; } // integer
		[Column("GiveawayBanner"                                                                                                      )] public string?   GiveawayBanner                     { get; set; } // text
		[Column("GiveawayEmbedColor"                                                                                                  )] public string?   GiveawayEmbedColor                 { get; set; } // text
		[Column("GiveawayWinEmbedColor"                                                                                               )] public string?   GiveawayWinEmbedColor              { get; set; } // text
		[Column("DmOnGiveawayWin"                                                                                                     )] public bool      DmOnGiveawayWin                    { get; set; } // boolean
		[Column("GiveawayEndMessage"                                                                                                  )] public string?   GiveawayEndMessage                 { get; set; } // text
		[Column("GiveawayPingRole"                                                                                                    )] public ulong   GiveawayPingRole                   { get; set; } // numeric(20,0)
		[Column("StarboardAllowBots"                                                                                                  )] public bool      StarboardAllowBots                 { get; set; } // boolean
		[Column("StarboardRemoveOnDelete"                                                                                             )] public bool      StarboardRemoveOnDelete            { get; set; } // boolean
		[Column("StarboardRemoveOnReactionsClear"                                                                                     )] public bool      StarboardRemoveOnReactionsClear    { get; set; } // boolean
		[Column("StarboardRemoveOnBelowThreshold"                                                                                     )] public bool      StarboardRemoveOnBelowThreshold    { get; set; } // boolean
		[Column("UseStarboardBlacklist"                                                                                               )] public bool      UseStarboardBlacklist              { get; set; } // boolean
		[Column("StarboardCheckChannels"                                                                                              )] public string?   StarboardCheckChannels             { get; set; } // text
		[Column("VotesPassword"                                                                                                       )] public string?   VotesPassword                      { get; set; } // text
		[Column("VotesChannel"                                                                                                        )] public ulong   VotesChannel                       { get; set; } // numeric(20,0)
		[Column("VoteEmbed"                                                                                                           )] public string?   VoteEmbed                          { get; set; } // text
		[Column("SuggestionThreadType"                                                                                                )] public int       SuggestionThreadType               { get; set; } // integer
		[Column("ArchiveOnDeny"                                                                                                       )] public bool      ArchiveOnDeny                      { get; set; } // boolean
		[Column("ArchiveOnAccept"                                                                                                     )] public bool      ArchiveOnAccept                    { get; set; } // boolean
		[Column("ArchiveOnConsider"                                                                                                   )] public bool      ArchiveOnConsider                  { get; set; } // boolean
		[Column("ArchiveOnImplement"                                                                                                  )] public bool      ArchiveOnImplement                 { get; set; } // boolean
		[Column("SuggestButtonMessage"                                                                                                )] public string?   SuggestButtonMessage               { get; set; } // text
		[Column("SuggestButtonName"                                                                                                   )] public string?   SuggestButtonName                  { get; set; } // text
		[Column("SuggestButtonEmote"                                                                                                  )] public string?   SuggestButtonEmote                 { get; set; } // text
		[Column("ButtonRepostThreshold"                                                                                               )] public int       ButtonRepostThreshold              { get; set; } // integer
		[Column("SuggestCommandsType"                                                                                                 )] public int       SuggestCommandsType                { get; set; } // integer
		[Column("AcceptChannel"                                                                                                       )] public ulong   AcceptChannel                      { get; set; } // numeric(20,0)
		[Column("DenyChannel"                                                                                                         )] public ulong   DenyChannel                        { get; set; } // numeric(20,0)
		[Column("ConsiderChannel"                                                                                                     )] public ulong   ConsiderChannel                    { get; set; } // numeric(20,0)
		[Column("ImplementChannel"                                                                                                    )] public ulong   ImplementChannel                   { get; set; } // numeric(20,0)
		[Column("EmoteMode"                                                                                                           )] public int       EmoteMode                          { get; set; } // integer
		[Column("SuggestMessage"                                                                                                      )] public string?   SuggestMessage                     { get; set; } // text
		[Column("DenyMessage"                                                                                                         )] public string?   DenyMessage                        { get; set; } // text
		[Column("AcceptMessage"                                                                                                       )] public string?   AcceptMessage                      { get; set; } // text
		[Column("ImplementMessage"                                                                                                    )] public string?   ImplementMessage                   { get; set; } // text
		[Column("ConsiderMessage"                                                                                                     )] public string?   ConsiderMessage                    { get; set; } // text
		[Column("MinSuggestLength"                                                                                                    )] public int       MinSuggestLength                   { get; set; } // integer
		[Column("MaxSuggestLength"                                                                                                    )] public int       MaxSuggestLength                   { get; set; } // integer
		[Column("SuggestEmotes"                                                                                                       )] public string?   SuggestEmotes                      { get; set; } // text
		[Column("sugnum"                                                                                                              )] public ulong   Sugnum                             { get; set; } // numeric(20,0)
		[Column("sugchan"                                                                                                             )] public ulong   Sugchan                            { get; set; } // numeric(20,0)
		[Column("SuggestButtonChannel"                                                                                                )] public ulong   SuggestButtonChannel               { get; set; } // numeric(20,0)
		[Column("Emote1Style"                                                                                                         )] public int       Emote1Style                        { get; set; } // integer
		[Column("Emote2Style"                                                                                                         )] public int       Emote2Style                        { get; set; } // integer
		[Column("Emote3Style"                                                                                                         )] public int       Emote3Style                        { get; set; } // integer
		[Column("Emote4Style"                                                                                                         )] public int       Emote4Style                        { get; set; } // integer
		[Column("Emote5Style"                                                                                                         )] public int       Emote5Style                        { get; set; } // integer
		[Column("SuggestButtonMessageId"                                                                                              )] public ulong   SuggestButtonMessageId             { get; set; } // numeric(20,0)
		[Column("SuggestButtonRepostThreshold"                                                                                        )] public int       SuggestButtonRepostThreshold       { get; set; } // integer
		[Column("SuggestButtonColor"                                                                                                  )] public int       SuggestButtonColor                 { get; set; } // integer
		[Column("AfkMessage"                                                                                                          )] public string?   AfkMessage                         { get; set; } // text
		[Column("AutoBotRoleIds"                                                                                                      )] public string?   AutoBotRoleIds                     { get; set; } // text
		[Column("GBEnabled"                                                                                                           )] public int       GbEnabled                          { get; set; } // integer
		[Column("GBAction"                                                                                                            )] public bool      GbAction                           { get; set; } // boolean
		[Column("ConfessionLogChannel"                                                                                                )] public ulong   ConfessionLogChannel               { get; set; } // numeric(20,0)
		[Column("ConfessionChannel"                                                                                                   )] public ulong   ConfessionChannel                  { get; set; } // numeric(20,0)
		[Column("ConfessionBlacklist"                                                                                                 )] public string?   ConfessionBlacklist                { get; set; } // text
		[Column("MultiGreetType"                                                                                                      )] public int       MultiGreetType                     { get; set; } // integer
		[Column("MemberRole"                                                                                                          )] public ulong   MemberRole                         { get; set; } // numeric(20,0)
		[Column("TOpenMessage"                                                                                                        )] public string?   TOpenMessage                       { get; set; } // text
		[Column("GStartMessage"                                                                                                       )] public string?   GStartMessage                      { get; set; } // text
		[Column("GEndMessage"                                                                                                         )] public string?   GEndMessage                        { get; set; } // text
		[Column("GWinMessage"                                                                                                         )] public string?   GWinMessage                        { get; set; } // text
		[Column("WarnlogChannelId"                                                                                                    )] public ulong   WarnlogChannelId                   { get; set; } // numeric(20,0)
		[Column("MiniWarnlogChannelId"                                                                                                )] public ulong   MiniWarnlogChannelId               { get; set; } // numeric(20,0)
		[Column("SendBoostMessage"                                                                                                    )] public bool      SendBoostMessage                   { get; set; } // boolean
		[Column("GRolesBlacklist"                                                                                                     )] public string?   GRolesBlacklist                    { get; set; } // text
		[Column("GUsersBlacklist"                                                                                                     )] public string?   GUsersBlacklist                    { get; set; } // text
		[Column("BoostMessage"                                                                                                        )] public string?   BoostMessage                       { get; set; } // text
		[Column("BoostMessageChannelId"                                                                                               )] public ulong   BoostMessageChannelId              { get; set; } // numeric(20,0)
		[Column("BoostMessageDeleteAfter"                                                                                             )] public int       BoostMessageDeleteAfter            { get; set; } // integer
		[Column("GiveawayEmote"                                                                                                       )] public string?   GiveawayEmote                      { get; set; } // text
		[Column("TicketChannel"                                                                                                       )] public ulong   TicketChannel                      { get; set; } // numeric(20,0)
		[Column("TicketCategory"                                                                                                      )] public ulong   TicketCategory                     { get; set; } // numeric(20,0)
		[Column("snipeset"                                                                                                            )] public bool      Snipeset                           { get; set; } // boolean
		[Column("AfkLength"                                                                                                           )] public int       AfkLength                          { get; set; } // integer
		[Column("XpTxtTimeout"                                                                                                        )] public int       XpTxtTimeout                       { get; set; } // integer
		[Column("XpTxtRate"                                                                                                           )] public int       XpTxtRate                          { get; set; } // integer
		[Column("XpVoiceRate"                                                                                                         )] public int       XpVoiceRate                        { get; set; } // integer
		[Column("XpVoiceTimeout"                                                                                                      )] public int       XpVoiceTimeout                     { get; set; } // integer
		[Column("Stars"                                                                                                               )] public int       Stars                              { get; set; } // integer
		[Column("AfkType"                                                                                                             )] public int       AfkType                            { get; set; } // integer
		[Column("AfkDisabledChannels"                                                                                                 )] public string?   AfkDisabledChannels                { get; set; } // text
		[Column("AfkDel"                                                                                                              )] public string?   AfkDel                             { get; set; } // text
		[Column("AfkTimeout"                                                                                                          )] public int       AfkTimeout                         { get; set; } // integer
		[Column("Joins"                                                                                                               )] public ulong   Joins                              { get; set; } // numeric(20,0)
		[Column("Leaves"                                                                                                              )] public ulong   Leaves                             { get; set; } // numeric(20,0)
		[Column("Star2"                                                                                                               )] public string?   Star2                              { get; set; } // text
		[Column("StarboardChannel"                                                                                                    )] public ulong   StarboardChannel                   { get; set; } // numeric(20,0)
		[Column("RepostThreshold"                                                                                                     )] public int       RepostThreshold                    { get; set; } // integer
		[Column("PreviewLinks"                                                                                                        )] public int       PreviewLinks                       { get; set; } // integer
		[Column("ReactChannel"                                                                                                        )] public ulong   ReactChannel                       { get; set; } // numeric(20,0)
		[Column("fwarn"                                                                                                               )] public int       Fwarn                              { get; set; } // integer
		[Column("invwarn"                                                                                                             )] public int       Invwarn                            { get; set; } // integer
		[Column("removeroles"                                                                                                         )] public int       Removeroles                        { get; set; } // integer
		[Column("AutoDeleteByeMessages"                                                                                               )] public bool      AutoDeleteByeMessages              { get; set; } // boolean
		[Column("AutoDeleteByeMessagesTimer"                                                                                          )] public int       AutoDeleteByeMessagesTimer         { get; set; } // integer
		[Column("ByeMessageChannelId"                                                                                                 )] public ulong   ByeMessageChannelId                { get; set; } // numeric(20,0)
		[Column("LeaveHook"                                                                                                           )] public string?   LeaveHook                          { get; set; } // text
		[Column("SendDmGreetMessage"                                                                                                  )] public bool      SendDmGreetMessage                 { get; set; } // boolean
		[Column("DmGreetMessageText"                                                                                                  )] public string?   DmGreetMessageText                 { get; set; } // text
		[Column("SendChannelByeMessage"                                                                                               )] public bool      SendChannelByeMessage              { get; set; } // boolean
		[Column("ChannelByeMessageText"                                                                                               )] public string?   ChannelByeMessageText              { get; set; } // text
		[Column("ExclusiveSelfAssignedRoles"                                                                                          )] public bool      ExclusiveSelfAssignedRoles         { get; set; } // boolean
		[Column("AutoDeleteSelfAssignedRoleMessages"                                                                                  )] public bool      AutoDeleteSelfAssignedRoleMessages { get; set; } // boolean
		[Column("LogSettingId"                                                                                                        )] public int?      LogSettingId                       { get; set; } // integer
		[Column("VerbosePermissions"                                                                                                  )] public bool      VerbosePermissions                 { get; set; } // boolean
		[Column("PermissionRole"                                                                                                      )] public string?   PermissionRole                     { get; set; } // text
		[Column("FilterInvites"                                                                                                       )] public bool      FilterInvites                      { get; set; } // boolean
		[Column("FilterLinks"                                                                                                         )] public bool      FilterLinks                        { get; set; } // boolean
		[Column("FilterWords"                                                                                                         )] public bool      FilterWords                        { get; set; } // boolean
		[Column("MuteRoleName"                                                                                                        )] public string?   MuteRoleName                       { get; set; } // text
		[Column("CleverbotChannel"                                                                                                    )] public ulong   CleverbotChannel                   { get; set; } // numeric(20,0)
		[Column("Locale"                                                                                                              )] public string?   Locale                             { get; set; } // text
		[Column("TimeZoneId"                                                                                                          )] public string?   TimeZoneId                         { get; set; } // text
		[Column("WarningsInitialized"                                                                                                 )] public bool      WarningsInitialized                { get; set; } // boolean
		[Column("GameVoiceChannel"                                                                                                    )] public ulong?  GameVoiceChannel                   { get; set; } // numeric(20,0)
		[Column("VerboseErrors"                                                                                                       )] public bool      VerboseErrors                      { get; set; } // boolean
		[Column("NotifyStreamOffline"                                                                                                 )] public bool      NotifyStreamOffline                { get; set; } // boolean
		[Column("WarnExpireHours"                                                                                                     )] public int       WarnExpireHours                    { get; set; } // integer
		[Column("WarnExpireAction"                                                                                                    )] public int       WarnExpireAction                   { get; set; } // integer
		[Column("JoinGraphColor"                                                                                                      )] public long      JoinGraphColor                     { get; set; } // bigint
		[Column("LeaveGraphColor"                                                                                                     )] public long      LeaveGraphColor                    { get; set; } // bigint
		[Column("DateAdded"                                                                                                           )] public DateTime? DateAdded                          { get; set; } // timestamp (6) without time zone
		[Column("UseMessageCount"                                                                                                     )] public bool      UseMessageCount                    { get; set; } // boolean
		[Column("MinMessageLength"                                                                                                    )] public int       MinMessageLength                   { get; set; } // integer
		[Column("PatreonChannelId"                                                                                                    )] public ulong     PatreonChannelId                   { get; set; } // numeric(20,0)
		[Column("PatreonMessage"                                                                                                      )] public string?   PatreonMessage                     { get; set; } // text
		[Column("PatreonAnnouncementDay"                                                                                              )] public int       PatreonAnnouncementDay             { get; set; } // integer
		[Column("PatreonEnabled"                                                                                                      )] public bool      PatreonEnabled                     { get; set; } // boolean
		[Column("PatreonLastAnnouncement"                                                                                             )] public DateTime? PatreonLastAnnouncement            { get; set; } // timestamp (6) without time zone
		[Column("PatreonCampaignId"                                                                                                   )] public string?   PatreonCampaignId                  { get; set; } // text
		[Column("PatreonAccessToken"                                                                                                  )] public string?   PatreonAccessToken                 { get; set; } // text
		[Column("PatreonRefreshToken"                                                                                                 )] public string?   PatreonRefreshToken                { get; set; } // text
		[Column("PatreonTokenExpiry"                                                                                                  )] public DateTime? PatreonTokenExpiry                 { get; set; } // timestamp (6) without time zone
		[Column("PatreonRoleSync"                                                                                                     )] public bool      PatreonRoleSync                    { get; set; } // boolean
		[Column("PatreonGoalChannel"                                                                                                  )] public ulong     PatreonGoalChannel                 { get; set; } // numeric(20,0)
		[Column("PatreonStatsChannel"                                                                                                 )] public ulong     PatreonStatsChannel                { get; set; } // numeric(20,0)
	}
}
