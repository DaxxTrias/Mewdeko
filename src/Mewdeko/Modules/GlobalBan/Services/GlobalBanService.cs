﻿//
//
// namespace Mewdeko.Modules.GlobalBan.Services;
//
// public class GlobalBanService
// {
//     private readonly IDataConnectionFactory dbFactory;
//     public GlobalBanService(EventHandler handler, IDataConnectionFactory dbFactory)
//     {
//         this.dbFactory = dbFactory;
//         handler.UserJoined += OnUserJoined;
//     }
//
//     private async Task OnUserJoined(IGuildUser args)
//     {
//
//         var banEntry = await dbContext.GlobalBans.FirstOrDefaultAsync(x => x.UserId == args.Id);
//         var config = await dbContext.GlobalBanConfigs.FirstOrDefaultAsync(x => x.GuildId == args.GuildId);
//         if (banEntry is null || config is null) return;
//
//          if (banEntry != null && config != null)
//          {
//              var enabledBanTypes = banEntry.Type & config.BanTypes;
//              if (enabledBanTypes != GbType.None)
//              {
//                  var banReasons = Enum.GetValues(typeof(GbType))
//                      .Cast<GbType>()
//                      .Where(gbType => (enabledBanTypes & gbType) != 0);
//
//                  var banReasonsString = string.Join(", ", banReasons);
//                  var channel = await args.Guild.GetTextChannelAsync(config.GlobalBanLogChannel);
//                  if (config.UseRecommendedAction)
//                  {
//                      switch (banEntry.RecommendedAction)
//                      {
//                          case GBActionType.Kick:
//                              var eb = new EmbedBuilder
//                              {
//                                  Author = new EmbedAuthorBuilder
//                                  {
//                                      IconUrl = args.GetAvatarUrl(), Name = args.Username
//                                  },
//                                  Title = "Global Ban",
//                                  Description = $"{args.Mention} ({args.Id}) was kicked for {banReasonsString}",
//                                  Fields = new List<EmbedFieldBuilder>
//                                     {
//                                         new()
//                                         {
//                                             Name = "Proof",
//                                             Value = banEntry.Proof
//                                         },
//                                         new()
//                                         {
//                                             Name = "Reason",
//                                             Value = banEntry.Reason
//                                         }
//                                     },
//                                  Color = Mewdeko.OkColor
//                              };
//                              await channel.SendMessageAsync(embed: eb.Build());
//                              await args.KickAsync(options: new RequestOptions
//                              {
//                                  AuditLogReason = $"Global Ban: {banReasonsString.TrimTo(256)}"
//                              });
//                              break;
//                          case GBActionType.Ban:
//                              var eb1 = new EmbedBuilder
//                              {
//                                  Author = new EmbedAuthorBuilder
//                                  {
//                                      IconUrl = args.GetAvatarUrl(), Name = args.Username
//                                  },
//                                  Title = "Global Ban",
//                                  Description = $"{args.Mention} ({args.Id}) was banned for {banReasonsString}",
//                                  Fields = new List<EmbedFieldBuilder>
//                                  {
//                                      new()
//                                      {
//                                          Name = "Proof",
//                                          Value = banEntry.Proof
//                                      },
//                                      new()
//                                      {
//                                          Name = "Reason",
//                                          Value = banEntry.Reason
//                                      }
//                                  },
//                                  Color = Mewdeko.OkColor
//                              };
//                              await channel.SendMessageAsync(embed: eb1.Build());
//                              await args.BanAsync(options: new RequestOptions
//                              {
//                                  AuditLogReason = $"Global Ban: {banReasonsString.TrimTo(256)}"
//                              });
//                              break;
//                          case GBActionType.Timeout:
//                              var eb2 = new EmbedBuilder
//                              {
//                                  Author = new EmbedAuthorBuilder
//                                  {
//                                      IconUrl = args.GetAvatarUrl(), Name = args.Username
//                                  },
//                                  Title = "Global Ban",
//                                  Description = $"{args.Mention} ({args.Id}) was timed out for {banReasonsString}",
//                                  Fields = new List<EmbedFieldBuilder>
//                                  {
//                                      new()
//                                      {
//                                          Name = "Proof",
//                                          Value = banEntry.Proof
//                                      },
//                                      new()
//                                      {
//                                          Name = "Reason",
//                                          Value = banEntry.Reason
//                                      }
//                                  },
//                                  Color = Mewdeko.OkColor
//                              };
//                              await channel.SendMessageAsync(embed: eb2.Build());
//                              await args.SetTimeOutAsync(banEntry.Duration, options: new RequestOptions
//                              {
//                                  AuditLogReason = $"Global Ban: {banReasonsString.TrimTo(256)}"
//                              });
//                              break;
//                          case GBActionType.None:
//                              var eb3 = new EmbedBuilder
//                              {
//                                  Author = new EmbedAuthorBuilder
//                                  {
//                                      IconUrl = args.GetAvatarUrl(), Name = args.Username
//                                  },
//                                  Title = "Global Ban",
//                                  Description = $"{args.Mention} ({args.Id}) was global banned for {banReasonsString}",
//                                  Fields = new List<EmbedFieldBuilder>
//                                  {
//                                      new()
//                                      {
//                                          Name = "Proof",
//                                          Value = banEntry.Proof
//                                      },
//                                      new()
//                                      {
//                                          Name = "Reason",
//                                          Value = banEntry.Reason
//                                      }
//                                  },
//                                  Color = Mewdeko.OkColor
//                              };
//                              await channel.SendMessageAsync(embed: eb3.Build());
//                              break;
//                      }
//                  }
//              }
//          }
//
//     }
// }

