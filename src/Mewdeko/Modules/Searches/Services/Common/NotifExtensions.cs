using DataModel;
using Mewdeko.Database.Common;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

namespace Mewdeko.Modules.Searches.Services.Common;

/// <summary>
/// Extensions for notifs
/// </summary>
public static class NotifExtensions
{
    /// <summary>
    ///     Creates a key for the stream data.
    /// </summary>
    /// <returns>A key for the stream data.</returns>
    public static StreamDataKey CreateKey(this FollowedStream stream)
    {
        return new StreamDataKey((FType)stream.Type, stream.Username.ToLower());
    }
}