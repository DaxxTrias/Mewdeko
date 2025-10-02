using System.Text;
using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Todo.Services;

namespace Mewdeko.Modules.Todo;

/// <summary>
///     Commands for managing todo lists and items with comprehensive permission system.
/// </summary>
public class Todo : MewdekoModuleBase<TodoService>
{
    /// <summary>
    ///     Creates a new todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoCreateList(string name, [Remainder] string? description = null)
    {
        var todoList = await Service.CreateTodoListAsync(ctx.Guild.Id, ctx.User.Id, name, description);

        if (todoList is null)
        {
            await ErrorAsync(Strings.TodoListExists(ctx.Guild.Id, name));
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoListCreated(ctx.Guild.Id))
            .WithDescription(Strings.TodoListDetails(ctx.Guild.Id, todoList.Name,
                todoList.Description ?? "No description"))
            .WithColor(Color.Green)
            .WithTimestamp(todoList.CreatedAt);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Creates a new server-wide todo list (admin only)
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task TodoCreateServerList(string name, [Remainder] string? description = null)
    {
        var todoList = await Service.CreateTodoListAsync(ctx.Guild.Id, ctx.User.Id, name, description,
            true);

        if (todoList is null)
        {
            await ErrorAsync(Strings.TodoServerListExists(ctx.Guild.Id, name));
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoServerListCreated(ctx.Guild.Id))
            .WithDescription(Strings.TodoListDetails(ctx.Guild.Id, todoList.Name,
                todoList.Description ?? "No description"))
            .WithColor(Color.Blue)
            .WithTimestamp(todoList.CreatedAt);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Lists all todo lists accessible to the user
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoLists(bool includeServer = true)
    {
        var lists = await Service.GetUserTodoListsAsync(ctx.Guild.Id, ctx.User.Id, includeServer);

        if (lists.Count == 0)
        {
            await ErrorAsync(Strings.TodoNoListsFound(ctx.Guild.Id));
            return;
        }

        var sb = new StringBuilder();
        foreach (var list in lists)
        {
            var type = list.IsServerList ? "🌐" : "👤";
            var visibility = list.IsPublic ? "👁️" : "🔒";
            sb.AppendLine($"{type}{visibility} **{list.Name}** (ID: {list.Id})");
            if (!string.IsNullOrEmpty(list.Description))
                sb.AppendLine($"   _{list.Description}_");
            sb.AppendLine();
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoListsTitle(ctx.Guild.Id))
            .WithDescription(sb.ToString())
            .WithColor(Color.Purple)
            .WithFooter(Strings.TodoListsFooter(ctx.Guild.Id));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Adds a new todo item to a specified list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoAdd(int listId, string title, [Remainder] string? description = null)
    {
        var item = await Service.AddTodoItemAsync(listId, ctx.User.Id, title, description);

        if (item is null)
        {
            await ErrorAsync(Strings.TodoAddFailed(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoItemAdded(ctx.Guild.Id))
            .WithDescription(Strings.TodoItemDetails(ctx.Guild.Id, item.Title, item.Description ?? "No description"))
            .WithColor(Color.Green)
            .WithTimestamp(item.CreatedAt);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Adds a todo item with priority and optional due date
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoAddPriority(int listId, int priority, string title, [Remainder] string? description = null)
    {
        var item = await Service.AddTodoItemAsync(listId, ctx.User.Id, title, description, priority);

        if (item is null)
        {
            await ErrorAsync(Strings.TodoAddFailed(ctx.Guild.Id));
            return;
        }

        var priorityText = priority switch
        {
            4 => "🔴 Critical",
            3 => "🟡 High",
            2 => "🟠 Medium",
            _ => "🟢 Low"
        };

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoItemAdded(ctx.Guild.Id))
            .WithDescription(Strings.TodoItemDetailsWithPriority(ctx.Guild.Id, item.Title, priorityText,
                item.Description ?? "No description"))
            .WithColor(Color.Green)
            .WithTimestamp(item.CreatedAt);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows all items in a todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoShow(int listId, bool includeCompleted = false)
    {
        var list = await Service.GetTodoListAsync(listId, ctx.User.Id, ctx.Guild.Id);
        if (list is null)
        {
            await ErrorAsync(Strings.TodoListNotFound(ctx.Guild.Id));
            return;
        }

        var items = await Service.GetTodoItemsAsync(listId, ctx.User.Id, includeCompleted);

        if (items.Count == 0)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle(Strings.TodoListTitle(ctx.Guild.Id, list.Name))
                .WithDescription(Strings.TodoListEmpty(ctx.Guild.Id))
                .WithColor(Color.LightGrey);
            await ctx.Channel.SendMessageAsync(embed: embedBuilder.Build());
            return;
        }

        var pendingItems = items.Where(x => !x.IsCompleted).ToList();
        var completedItems = items.Where(x => x.IsCompleted).ToList();

        var sb = new StringBuilder();

        if (pendingItems.Count > 0)
        {
            sb.AppendLine($"**{Strings.TodoPendingItems(ctx.Guild.Id)} ({pendingItems.Count})**");
            foreach (var item in pendingItems)
            {
                var priority = item.Priority switch
                {
                    4 => "🔴",
                    3 => "🟡",
                    2 => "🟠",
                    _ => "🟢"
                };
                var dueText = item.DueDate.HasValue
                    ? $" ⏰ <t:{((DateTimeOffset)item.DueDate.Value).ToUnixTimeSeconds()}:R>"
                    : "";
                sb.AppendLine($"{priority} `{item.Id}` **{item.Title}**{dueText}");
                if (!string.IsNullOrEmpty(item.Description))
                    sb.AppendLine($"   _{item.Description}_");
            }
        }

        if (includeCompleted && completedItems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**{Strings.TodoCompletedItems(ctx.Guild.Id)} ({completedItems.Count})**");
            foreach (var item in completedItems.Take(5)) // Limit completed items shown
            {
                sb.AppendLine(
                    $"✅ ~~{item.Title}~~ - <t:{((DateTimeOffset)item.CompletedAt!.Value).ToUnixTimeSeconds()}:R>");
            }

            if (completedItems.Count > 5)
                sb.AppendLine($"... {Strings.TodoAndMore(ctx.Guild.Id, completedItems.Count - 5)}");
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoListTitle(ctx.Guild.Id, list.Name))
            .WithDescription(sb.ToString())
            .WithColor(Color.Parse(list.Color ?? "#7289da"))
            .WithFooter(Strings.TodoShowFooter(ctx.Guild.Id, listId));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Completes a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoComplete(int itemId)
    {
        var success = await Service.CompleteTodoItemAsync(itemId, ctx.User.Id);

        if (!success)
        {
            await ErrorAsync(Strings.TodoCompleteFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoItemCompleted(ctx.Guild.Id));
    }

    /// <summary>
    ///     Deletes a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoDelete(int itemId)
    {
        var success = await Service.DeleteTodoItemAsync(itemId, ctx.User.Id);

        if (!success)
        {
            await ErrorAsync(Strings.TodoDeleteFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoItemDeleted(ctx.Guild.Id));
    }

    /// <summary>
    ///     Deletes an entire todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoDeleteList(int listId)
    {
        var success = await Service.DeleteTodoListAsync(listId, ctx.User.Id);

        if (!success)
        {
            await ErrorAsync(Strings.TodoDeleteListFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoListDeleted(ctx.Guild.Id));
    }

    /// <summary>
    ///     Grants permissions to a user for a todo list (list owner/admin only)
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoGrant(int listId, IUser user, string permissions)
    {
        var perms = permissions.ToLower().Split(',', StringSplitOptions.RemoveEmptyEntries);

        var canView = perms.Contains("view") || perms.Contains("all");
        var canAdd = perms.Contains("add") || perms.Contains("all");
        var canEdit = perms.Contains("edit") || perms.Contains("all");
        var canComplete = perms.Contains("complete") || perms.Contains("all");
        var canDelete = perms.Contains("delete") || perms.Contains("all");
        var canManage = perms.Contains("manage") || perms.Contains("all");

        var success = await Service.GrantPermissionsAsync(listId, user.Id, ctx.User.Id,
            canView, canAdd, canEdit, canComplete, canDelete, canManage);

        if (!success)
        {
            await ErrorAsync(Strings.TodoGrantFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoPermissionsGranted(ctx.Guild.Id, user.Mention, string.Join(", ", perms)));
    }

    /// <summary>
    ///     Sets the due date for a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoSetDue(int itemId, [Remainder] string dateString)
    {
        if (!DateTime.TryParse(dateString, out var dueDate))
        {
            await ErrorAsync(Strings.TodoInvalidDate(ctx.Guild.Id));
            return;
        }

        var success = await Service.SetTodoItemDueDateAsync(itemId, ctx.User.Id, dueDate);

        if (!success)
        {
            await ErrorAsync(Strings.TodoSetDueFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoDueDateSet(ctx.Guild.Id, dueDate.ToString("yyyy-MM-dd HH:mm")));
    }

    /// <summary>
    ///     Removes the due date from a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoClearDue(int itemId)
    {
        var success = await Service.SetTodoItemDueDateAsync(itemId, ctx.User.Id, null);

        if (!success)
        {
            await ErrorAsync(Strings.TodoClearDueFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoDueDateCleared(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets a reminder time for a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoSetReminder(int itemId, [Remainder] string dateString)
    {
        if (!DateTime.TryParse(dateString, out var reminderTime))
        {
            await ErrorAsync(Strings.TodoInvalidDate(ctx.Guild.Id));
            return;
        }

        var success = await Service.SetTodoItemReminderAsync(itemId, ctx.User.Id, reminderTime);

        if (!success)
        {
            await ErrorAsync(Strings.TodoSetReminderFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoReminderSet(ctx.Guild.Id, reminderTime.ToString("yyyy-MM-dd HH:mm")));
    }

    /// <summary>
    ///     Removes the reminder from a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoClearReminder(int itemId)
    {
        var success = await Service.SetTodoItemReminderAsync(itemId, ctx.User.Id, null);

        if (!success)
        {
            await ErrorAsync(Strings.TodoClearReminderFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoReminderCleared(ctx.Guild.Id));
    }

    /// <summary>
    ///     Adds a tag to a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoAddTag(int itemId, [Remainder] string tag)
    {
        var success = await Service.AddTagToTodoItemAsync(itemId, ctx.User.Id, tag);

        if (!success)
        {
            await ErrorAsync(Strings.TodoAddTagFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoTagAdded(ctx.Guild.Id, tag));
    }

    /// <summary>
    ///     Removes a tag from a todo item
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoRemoveTag(int itemId, [Remainder] string tag)
    {
        var success = await Service.RemoveTagFromTodoItemAsync(itemId, ctx.User.Id, tag);

        if (!success)
        {
            await ErrorAsync(Strings.TodoRemoveTagFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoTagRemoved(ctx.Guild.Id, tag));
    }

    /// <summary>
    ///     Edits a todo item's title and description
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoEdit(int itemId, string title, [Remainder] string? description = null)
    {
        var success = await Service.EditTodoItemAsync(itemId, ctx.User.Id, title, description);

        if (!success)
        {
            await ErrorAsync(Strings.TodoEditFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoItemEdited(ctx.Guild.Id));
    }

    /// <summary>
    ///     Reorders a todo item to a new position
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoReorder(int itemId, int newPosition)
    {
        var success = await Service.ReorderTodoItemAsync(itemId, ctx.User.Id, newPosition);

        if (!success)
        {
            await ErrorAsync(Strings.TodoReorderFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoItemReordered(ctx.Guild.Id, newPosition));
    }

    /// <summary>
    ///     Sets the color for a todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoSetColor(int listId, [Remainder] string color)
    {
        // Validate hex color format
        if (!color.StartsWith("#") || color.Length != 7)
        {
            await ErrorAsync(Strings.TodoInvalidColor(ctx.Guild.Id));
            return;
        }

        var success = await Service.SetTodoListColorAsync(listId, ctx.User.Id, color);

        if (!success)
        {
            await ErrorAsync(Strings.TodoSetColorFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoColorSet(ctx.Guild.Id, color));
    }

    /// <summary>
    ///     Toggles the privacy setting for a todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoTogglePrivacy(int listId)
    {
        var newPrivacy = await Service.ToggleTodoListPrivacyAsync(listId, ctx.User.Id);

        if (newPrivacy is null)
        {
            await ErrorAsync(Strings.TodoTogglePrivacyFailed(ctx.Guild.Id));
            return;
        }

        var privacyText = newPrivacy.Value ? "public" : "private";
        await ConfirmAsync(Strings.TodoPrivacyToggled(ctx.Guild.Id, privacyText));
    }

    /// <summary>
    ///     Shows all permissions for a todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoPermissions(int listId)
    {
        var permissions = await Service.GetTodoListPermissionsAsync(listId, ctx.User.Id);

        if (permissions is null)
        {
            await ErrorAsync(Strings.TodoPermissionsViewFailed(ctx.Guild.Id));
            return;
        }

        if (permissions.Count == 0)
        {
            await ErrorAsync(Strings.TodoNoPermissionsFound(ctx.Guild.Id));
            return;
        }

        var sb = new StringBuilder();
        foreach (var perm in permissions)
        {
            var user = await ctx.Client.GetUserAsync(perm.UserId);
            var userName = user?.Username ?? $"Unknown User ({perm.UserId})";
            var permsList = new List<string>();

            if (perm.CanView) permsList.Add("view");
            if (perm.CanAdd) permsList.Add("add");
            if (perm.CanEdit) permsList.Add("edit");
            if (perm.CanComplete) permsList.Add("complete");
            if (perm.CanDelete) permsList.Add("delete");
            if (perm.CanManageList) permsList.Add("manage");

            sb.AppendLine($"**{userName}**: {string.Join(", ", permsList)}");
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoPermissionsTitle(ctx.Guild.Id))
            .WithDescription(sb.ToString())
            .WithColor(Color.Blue);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Revokes all permissions for a user on a todo list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoRevoke(int listId, IUser user)
    {
        var success = await Service.RevokeTodoListPermissionsAsync(listId, user.Id, ctx.User.Id);

        if (!success)
        {
            await ErrorAsync(Strings.TodoRevokeFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.TodoPermissionsRevoked(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Searches for todo items within a list
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoSearch(int listId, [Remainder] string query)
    {
        var items = await Service.SearchTodoItemsAsync(listId, ctx.User.Id, query, false);

        if (items is null)
        {
            await ErrorAsync(Strings.TodoSearchFailed(ctx.Guild.Id));
            return;
        }

        if (items.Count == 0)
        {
            await ErrorAsync(Strings.TodoSearchNoResults(ctx.Guild.Id, query));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**{Strings.TodoSearchResults(ctx.Guild.Id, items.Count, query)}**");

        foreach (var item in items.Take(10)) // Limit to 10 results
        {
            var priority = item.Priority switch
            {
                4 => "🔴",
                3 => "🟡",
                2 => "🟠",
                _ => "🟢"
            };
            var dueText = item.DueDate.HasValue
                ? $" ⏰ <t:{((DateTimeOffset)item.DueDate.Value).ToUnixTimeSeconds()}:R>"
                : "";
            sb.AppendLine($"{priority} `{item.Id}` **{item.Title}**{dueText}");
            if (!string.IsNullOrEmpty(item.Description))
                sb.AppendLine($"   _{item.Description}_");
        }

        if (items.Count > 10)
            sb.AppendLine($"... {Strings.TodoAndMore(ctx.Guild.Id, items.Count - 10)}");

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoSearchTitle(ctx.Guild.Id))
            .WithDescription(sb.ToString())
            .WithColor(Color.Orange);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Filters todo items by tag or priority
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TodoFilter(int listId, string filter, [Remainder] string? value = null)
    {
        List<TodoItem>? items = null;

        switch (filter.ToLower())
        {
            case "tag":
                if (string.IsNullOrEmpty(value))
                {
                    await ErrorAsync(Strings.TodoFilterTagRequired(ctx.Guild.Id));
                    return;
                }

                items = await Service.FilterTodoItemsAsync(listId, ctx.User.Id, value, includeCompleted: false);
                break;

            case "priority":
                if (!int.TryParse(value, out var priority) || priority < 1 || priority > 4)
                {
                    await ErrorAsync(Strings.TodoFilterPriorityInvalid(ctx.Guild.Id));
                    return;
                }

                items = await Service.FilterTodoItemsAsync(listId, ctx.User.Id, priority: priority,
                    includeCompleted: false);
                break;

            default:
                await ErrorAsync(Strings.TodoFilterInvalidType(ctx.Guild.Id));
                return;
        }

        if (items is null)
        {
            await ErrorAsync(Strings.TodoFilterFailed(ctx.Guild.Id));
            return;
        }

        if (items.Count == 0)
        {
            await ErrorAsync(Strings.TodoFilterNoResults(ctx.Guild.Id, filter, value ?? ""));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**{Strings.TodoFilterResults(ctx.Guild.Id, items.Count, filter, value ?? "")}**");

        foreach (var item in items.Take(10)) // Limit to 10 results
        {
            var priority = item.Priority switch
            {
                4 => "🔴",
                3 => "🟡",
                2 => "🟠",
                _ => "🟢"
            };
            var tags = item.Tags?.Length > 0 ? $" [{string.Join(", ", item.Tags)}]" : "";
            var dueText = item.DueDate.HasValue
                ? $" ⏰ <t:{((DateTimeOffset)item.DueDate.Value).ToUnixTimeSeconds()}:R>"
                : "";
            sb.AppendLine($"{priority} `{item.Id}` **{item.Title}**{tags}{dueText}");
            if (!string.IsNullOrEmpty(item.Description))
                sb.AppendLine($"   _{item.Description}_");
        }

        if (items.Count > 10)
            sb.AppendLine($"... {Strings.TodoAndMore(ctx.Guild.Id, items.Count - 10)}");

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoFilterTitle(ctx.Guild.Id))
            .WithDescription(sb.ToString())
            .WithColor(Color.Gold);

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }
}