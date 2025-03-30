using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Utility.Services
{
    /// <summary>
    /// Service for managing chat logs
    /// </summary>
    public class ChatLogService(DbContextProvider db) : INService
    {
        /// <summary>
        /// Saves a chat log
        /// </summary>
        public async Task<string> SaveChatLogAsync(
            ulong guildId,
            ulong channelId,
            string channelName,
            string name,
            ulong createdBy,
            IEnumerable<object> messages)
        {
            try
            {
                var messagesArray = messages.ToArray();
                var chatLog = new ChatLog
                {
                    GuildId = guildId,
                    ChannelId = channelId,
                    ChannelName = channelName,
                    Name = name,
                    CreatedBy = createdBy,
                    Messages = JsonConvert.SerializeObject(messagesArray),
                    MessageCount = messagesArray.Length,
                    DateAdded = DateTime.UtcNow
                };

                await using var context = await db.GetContextAsync();
                await context.ChatLogs.AddAsync(chatLog);
                await context.SaveChangesAsync();

                return chatLog.Id.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving chat log");
                throw;
            }
        }

        /// <summary>
        /// Gets a chat log by id
        /// </summary>
        public async Task<ChatLog?> GetChatLogAsync(int logId)
        {
            try
            {
                await using var context = await db.GetContextAsync();
                return await context.ChatLogs.FindAsync(logId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting chat log");
                throw;
            }
        }

        /// <summary>
        /// Gets all chat logs for a guild
        /// </summary>
        public async Task<IEnumerable<ChatLog>> GetChatLogsForGuildAsync(ulong guildId)
        {
            try
            {
                await using var context = await db.GetContextAsync();
                return await context.ChatLogs
                    .Where(l => l.GuildId == guildId)
                    .OrderByDescending(l => l.DateAdded)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting chat logs for guild");
                throw;
            }
        }

        /// <summary>
        /// Updates a chat log's name
        /// </summary>
        public async Task UpdateChatLogNameAsync(int logId, string newName)
        {
            try
            {
                await using var context = await db.GetContextAsync();
                var log = await context.ChatLogs.FindAsync(logId);

                if (log == null)
                    throw new Exception("Chat log not found");

                log.Name = newName;
                context.ChatLogs.Update(log);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating chat log name");
                throw;
            }
        }

        /// <summary>
        /// Deletes a chat log
        /// </summary>
        public async Task DeleteChatLogAsync(int logId)
        {
            try
            {
                await using var context = await db.GetContextAsync();
                var log = await context.ChatLogs.FindAsync(logId);

                if (log == null)
                    return;

                context.ChatLogs.Remove(log);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting chat log");
                throw;
            }
        }
    }
}