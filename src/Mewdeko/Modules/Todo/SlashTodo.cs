using System.Text;
using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Todo.Services;

namespace Mewdeko.Modules.Todo;

/// <summary>
///     Provides slash commands for managing the todo system.
/// </summary>
[Group("todo", "Manage todo lists and items")]
public class TodoSlash : MewdekoSlashModuleBase<TodoService>
{
    private readonly InteractiveService interactivity;
    private readonly ILogger<TodoSlash> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TodoSlash" /> class.
    /// </summary>
    public TodoSlash(InteractiveService interactivity, ILogger<TodoSlash> logger)
    {
        this.interactivity = interactivity;
        this.logger = logger;
    }

    /// <summary>
    ///     Creates a new personal todo list.
    /// </summary>
    [SlashCommand("createlist", "Creates a new personal todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task CreateList(
        [Summary("name", "The name of the todo list")] [MaxLength(50)]
        string name,
        [Summary("description", "Optional description for the list")] [MaxLength(200)]
        string? description = null
    )
    {
        await DeferAsync();

        var todoList = await Service.CreateTodoListAsync(ctx.Guild.Id, ctx.User.Id, name, description);

        if (todoList is null)
        {
            await FollowupAsync(Strings.TodoListExists(ctx.Guild.Id, name), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoListCreated(ctx.Guild.Id))
            .WithDescription(Strings.TodoListDetails(ctx.Guild.Id, todoList.Name,
                todoList.Description ?? "No description"))
            .WithColor(Color.Green)
            .WithTimestamp(todoList.CreatedAt);

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Creates a new server-wide todo list (admin only).
    /// </summary>
    [SlashCommand("createserverlist", "Creates a new server-wide todo list")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [RequireContext(ContextType.Guild)]
    public async Task CreateServerList(
        [Summary("name", "The name of the server todo list")] [MaxLength(50)]
        string name,
        [Summary("description", "Optional description for the list")] [MaxLength(200)]
        string? description = null
    )
    {
        await DeferAsync();

        var todoList = await Service.CreateTodoListAsync(ctx.Guild.Id, ctx.User.Id, name, description,
            true, true);

        if (todoList is null)
        {
            await FollowupAsync(Strings.TodoServerListExists(ctx.Guild.Id, name), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.TodoServerListCreated(ctx.Guild.Id))
            .WithDescription(Strings.TodoListDetails(ctx.Guild.Id, todoList.Name,
                todoList.Description ?? "No description"))
            .WithColor(Color.Blue)
            .WithTimestamp(todoList.CreatedAt);

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Lists all accessible todo lists.
    /// </summary>
    [SlashCommand("lists", "Shows all your accessible todo lists")]
    [RequireContext(ContextType.Guild)]
    public async Task Lists(
        [Summary("include-server", "Include server-wide lists")]
        bool includeServer = true
    )
    {
        await DeferAsync();

        var lists = await Service.GetUserTodoListsAsync(ctx.Guild.Id, ctx.User.Id, includeServer);

        if (lists.Count == 0)
        {
            await FollowupAsync(Strings.TodoNoListsFound(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var pages = new List<PageBuilder>();
        const int itemsPerPage = 10;

        for (var i = 0; i < lists.Count; i += itemsPerPage)
        {
            var pageItems = lists.Skip(i).Take(itemsPerPage);
            var sb = new StringBuilder();

            foreach (var list in pageItems)
            {
                var type = list.IsServerList ? "🌐" : "👤";
                var visibility = list.IsPublic ? "👁️" : "🔒";
                sb.AppendLine($"{type}{visibility} **{list.Name}** (ID: {list.Id})");
                if (!string.IsNullOrEmpty(list.Description))
                    sb.AppendLine($"   _{list.Description}_");
                sb.AppendLine();
            }

            var page = new PageBuilder()
                .WithTitle(Strings.TodoListsTitle(ctx.Guild.Id))
                .WithDescription(sb.ToString())
                .WithColor(Color.Purple)
                .WithFooter(
                    $"Page {(i / itemsPerPage) + 1}/{(lists.Count - 1) / itemsPerPage + 1} • {Strings.TodoListsFooter(ctx.Guild.Id)}");

            pages.Add(page);
        }

        var paginator = new StaticPaginatorBuilder()
            .WithPages(pages)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithUsers(ctx.User)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteInput)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    ///     Adds a new todo item to a list.
    /// </summary>
    [SlashCommand("add", "Adds a new todo item to a list")]
    [RequireContext(ContextType.Guild)]
    public async Task Add(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("title", "The title of the todo item")] [MaxLength(100)]
        string title,
        [Summary("description", "Optional description")] [MaxLength(500)]
        string? description = null,
        [Summary("priority", "Priority level (1=Low, 2=Medium, 3=High, 4=Critical)")]
        [Choice("Low", 1)]
        [Choice("Medium", 2)]
        [Choice("High", 3)]
        [Choice("Critical", 4)]
        int priority = 1
    )
    {
        await DeferAsync();

        var item = await Service.AddTodoItemAsync(listId, ctx.User.Id, title, description, priority);

        if (item is null)
        {
            await FollowupAsync(Strings.TodoAddFailed(ctx.Guild.Id), ephemeral: true);
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

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows all items in a todo list.
    /// </summary>
    [SlashCommand("show", "Shows all items in a todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task Show(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("include-completed", "Include completed items")]
        bool includeCompleted = false
    )
    {
        await DeferAsync();

        var list = await Service.GetTodoListAsync(listId, ctx.User.Id, ctx.Guild.Id);
        if (list is null)
        {
            await FollowupAsync(Strings.TodoListNotFound(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var items = await Service.GetTodoItemsAsync(listId, ctx.User.Id, includeCompleted);

        if (items.Count == 0)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"📝 {list.Name}")
                .WithDescription(Strings.TodoListEmpty(ctx.Guild.Id))
                .WithColor(Color.LightGrey);
            await FollowupAsync(embed: embed.Build());
            return;
        }

        var pendingItems = items.Where(x => !x.IsCompleted).ToList();
        var completedItems = items.Where(x => x.IsCompleted).ToList();

        var pages = new List<PageBuilder>();
        const int itemsPerPage = 8;

        // Create pages for pending items
        if (pendingItems.Count > 0)
        {
            for (var i = 0; i < pendingItems.Count; i += itemsPerPage)
            {
                var pageItems = pendingItems.Skip(i).Take(itemsPerPage);
                var sb = new StringBuilder();
                sb.AppendLine($"**{Strings.TodoPendingItems(ctx.Guild.Id)} ({pendingItems.Count})**");

                foreach (var item in pageItems)
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

                var page = new PageBuilder()
                    .WithTitle($"📝 {list.Name}")
                    .WithDescription(sb.ToString())
                    .WithColor(Color.Parse(list.Color ?? "#7289da"))
                    .WithFooter($"Pending Page {(i / itemsPerPage) + 1}");

                pages.Add(page);
            }
        }

        // Add completed items page if requested
        if (includeCompleted && completedItems.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"**{Strings.TodoCompletedItems(ctx.Guild.Id)} ({completedItems.Count})**");
            foreach (var item in completedItems.Take(10)) // Limit completed items shown
            {
                sb.AppendLine(
                    $"✅ ~~{item.Title}~~ - <t:{((DateTimeOffset)item.CompletedAt!.Value).ToUnixTimeSeconds()}:R>");
            }

            if (completedItems.Count > 10)
                sb.AppendLine($"... {Strings.TodoAndMore(ctx.Guild.Id, completedItems.Count - 10)}");

            var completedPage = new PageBuilder()
                .WithTitle($"📝 {list.Name}")
                .WithDescription(sb.ToString())
                .WithColor(Color.Parse(list.Color ?? "#7289da"))
                .WithFooter("Completed Items");

            pages.Add(completedPage);
        }

        if (pages.Count == 0)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"📝 {list.Name}")
                .WithDescription(Strings.TodoListEmpty(ctx.Guild.Id))
                .WithColor(Color.LightGrey);
            await FollowupAsync(embed: embed.Build());
            return;
        }

        var paginator = new StaticPaginatorBuilder()
            .WithPages(pages)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithUsers(ctx.User)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteInput)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    ///     Marks a todo item as completed.
    /// </summary>
    [SlashCommand("complete", "Marks a todo item as completed")]
    [RequireContext(ContextType.Guild)]
    public async Task Complete(
        [Summary("item-id", "The ID of the todo item")]
        int itemId
    )
    {
        await DeferAsync();

        var success = await Service.CompleteTodoItemAsync(itemId, ctx.User.Id);

        if (!success)
        {
            await FollowupAsync(Strings.TodoCompleteFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoItemCompleted(ctx.Guild.Id));
    }

    /// <summary>
    ///     Deletes a todo item.
    /// </summary>
    [SlashCommand("delete", "Deletes a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task Delete(
        [Summary("item-id", "The ID of the todo item")]
        int itemId
    )
    {
        await DeferAsync();

        var success = await Service.DeleteTodoItemAsync(itemId, ctx.User.Id);

        if (!success)
        {
            await FollowupAsync(Strings.TodoDeleteFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoItemDeleted(ctx.Guild.Id));
    }

    /// <summary>
    ///     Deletes an entire todo list.
    /// </summary>
    [SlashCommand("deletelist", "Deletes an entire todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task DeleteList(
        [Summary("list-id", "The ID of the todo list")]
        int listId
    )
    {
        await DeferAsync();

        var success = await Service.DeleteTodoListAsync(listId, ctx.User.Id);

        if (!success)
        {
            await FollowupAsync(Strings.TodoDeleteListFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoListDeleted(ctx.Guild.Id));
    }

    /// <summary>
    ///     Grants permissions to a user for a todo list.
    /// </summary>
    [SlashCommand("grant", "Grants permissions to a user for a todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task Grant(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("user", "The user to grant permissions to")]
        IUser user,
        [Summary("view", "Can view the list")] bool canView = true,
        [Summary("add", "Can add items")] bool canAdd = false,
        [Summary("edit", "Can edit others' items")]
        bool canEdit = false,
        [Summary("complete", "Can complete others' items")]
        bool canComplete = false,
        [Summary("delete", "Can delete others' items")]
        bool canDelete = false,
        [Summary("manage", "Can manage list settings")]
        bool canManage = false
    )
    {
        await DeferAsync();

        var success = await Service.GrantPermissionsAsync(listId, user.Id, ctx.User.Id,
            canView, canAdd, canEdit, canComplete, canDelete, canManage);

        if (!success)
        {
            await FollowupAsync(Strings.TodoGrantFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var permissions = new List<string>();
        if (canView) permissions.Add("view");
        if (canAdd) permissions.Add("add");
        if (canEdit) permissions.Add("edit");
        if (canComplete) permissions.Add("complete");
        if (canDelete) permissions.Add("delete");
        if (canManage) permissions.Add("manage");

        await FollowupAsync(Strings.TodoPermissionsGranted(ctx.Guild.Id, user.Mention, string.Join(", ", permissions)));
    }

    /// <summary>
    ///     Sets the due date for a todo item.
    /// </summary>
    [SlashCommand("setdue", "Sets the due date for a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task SetDue(
        [Summary("item-id", "The ID of the todo item")]
        int itemId,
        [Summary("year", "Year (e.g., 2024)")] int year,
        [Summary("month", "Month (1-12)")] int month,
        [Summary("day", "Day (1-31)")] int day,
        [Summary("hour", "Hour (0-23)")] int hour = 12,
        [Summary("minute", "Minute (0-59)")] int minute = 0
    )
    {
        await DeferAsync();

        try
        {
            var dueDate = new DateTime(year, month, day, hour, minute, 0);
            var success = await Service.SetTodoItemDueDateAsync(itemId, ctx.User.Id, dueDate);

            if (!success)
            {
                await FollowupAsync(Strings.TodoSetDueFailed(ctx.Guild.Id), ephemeral: true);
                return;
            }

            await FollowupAsync(Strings.TodoDueDateSet(ctx.Guild.Id, dueDate.ToString("yyyy-MM-dd HH:mm")));
        }
        catch (ArgumentException)
        {
            await FollowupAsync(Strings.TodoInvalidDate(ctx.Guild.Id), ephemeral: true);
        }
    }

    /// <summary>
    ///     Removes the due date from a todo item.
    /// </summary>
    [SlashCommand("cleardue", "Removes the due date from a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task ClearDue(
        [Summary("item-id", "The ID of the todo item")]
        int itemId
    )
    {
        await DeferAsync();

        var success = await Service.SetTodoItemDueDateAsync(itemId, ctx.User.Id, null);

        if (!success)
        {
            await FollowupAsync(Strings.TodoClearDueFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoDueDateCleared(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets a reminder time for a todo item.
    /// </summary>
    [SlashCommand("setreminder", "Sets a reminder time for a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task SetReminder(
        [Summary("item-id", "The ID of the todo item")]
        int itemId,
        [Summary("year", "Year (e.g., 2024)")] int year,
        [Summary("month", "Month (1-12)")] int month,
        [Summary("day", "Day (1-31)")] int day,
        [Summary("hour", "Hour (0-23)")] int hour = 12,
        [Summary("minute", "Minute (0-59)")] int minute = 0
    )
    {
        await DeferAsync();

        try
        {
            var reminderTime = new DateTime(year, month, day, hour, minute, 0);
            var success = await Service.SetTodoItemReminderAsync(itemId, ctx.User.Id, reminderTime);

            if (!success)
            {
                await FollowupAsync(Strings.TodoSetReminderFailed(ctx.Guild.Id), ephemeral: true);
                return;
            }

            await FollowupAsync(Strings.TodoReminderSet(ctx.Guild.Id, reminderTime.ToString("yyyy-MM-dd HH:mm")));
        }
        catch (ArgumentException)
        {
            await FollowupAsync(Strings.TodoInvalidDate(ctx.Guild.Id), ephemeral: true);
        }
    }

    /// <summary>
    ///     Removes the reminder from a todo item.
    /// </summary>
    [SlashCommand("clearreminder", "Removes the reminder from a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task ClearReminder(
        [Summary("item-id", "The ID of the todo item")]
        int itemId
    )
    {
        await DeferAsync();

        var success = await Service.SetTodoItemReminderAsync(itemId, ctx.User.Id, null);

        if (!success)
        {
            await FollowupAsync(Strings.TodoClearReminderFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoReminderCleared(ctx.Guild.Id));
    }

    /// <summary>
    ///     Adds a tag to a todo item.
    /// </summary>
    [SlashCommand("addtag", "Adds a tag to a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task AddTag(
        [Summary("item-id", "The ID of the todo item")]
        int itemId,
        [Summary("tag", "The tag to add")] [MaxLength(30)]
        string tag
    )
    {
        await DeferAsync();

        var success = await Service.AddTagToTodoItemAsync(itemId, ctx.User.Id, tag);

        if (!success)
        {
            await FollowupAsync(Strings.TodoAddTagFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoTagAdded(ctx.Guild.Id, tag));
    }

    /// <summary>
    ///     Removes a tag from a todo item.
    /// </summary>
    [SlashCommand("removetag", "Removes a tag from a todo item")]
    [RequireContext(ContextType.Guild)]
    public async Task RemoveTag(
        [Summary("item-id", "The ID of the todo item")]
        int itemId,
        [Summary("tag", "The tag to remove")] string tag
    )
    {
        await DeferAsync();

        var success = await Service.RemoveTagFromTodoItemAsync(itemId, ctx.User.Id, tag);

        if (!success)
        {
            await FollowupAsync(Strings.TodoRemoveTagFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoTagRemoved(ctx.Guild.Id, tag));
    }

    /// <summary>
    ///     Edits a todo item's title and description.
    /// </summary>
    [SlashCommand("edit", "Edits a todo item's title and description")]
    [RequireContext(ContextType.Guild)]
    public async Task Edit(
        [Summary("item-id", "The ID of the todo item")]
        int itemId,
        [Summary("title", "The new title")] [MaxLength(100)]
        string title,
        [Summary("description", "The new description")] [MaxLength(500)]
        string? description = null
    )
    {
        await DeferAsync();

        var success = await Service.EditTodoItemAsync(itemId, ctx.User.Id, title, description);

        if (!success)
        {
            await FollowupAsync(Strings.TodoEditFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoItemEdited(ctx.Guild.Id));
    }

    /// <summary>
    ///     Reorders a todo item to a new position.
    /// </summary>
    [SlashCommand("reorder", "Reorders a todo item to a new position")]
    [RequireContext(ContextType.Guild)]
    public async Task Reorder(
        [Summary("item-id", "The ID of the todo item")]
        int itemId,
        [Summary("position", "The new position (1-based)")]
        int newPosition
    )
    {
        await DeferAsync();

        var success = await Service.ReorderTodoItemAsync(itemId, ctx.User.Id, newPosition);

        if (!success)
        {
            await FollowupAsync(Strings.TodoReorderFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoItemReordered(ctx.Guild.Id, newPosition));
    }

    /// <summary>
    ///     Sets the color for a todo list.
    /// </summary>
    [SlashCommand("setcolor", "Sets the color for a todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task SetColor(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("color", "Hex color code (e.g., #ff0000)")]
        string color
    )
    {
        await DeferAsync();

        // Validate hex color format
        if (!color.StartsWith("#") || color.Length != 7)
        {
            await FollowupAsync(Strings.TodoInvalidColor(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var success = await Service.SetTodoListColorAsync(listId, ctx.User.Id, color);

        if (!success)
        {
            await FollowupAsync(Strings.TodoSetColorFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoColorSet(ctx.Guild.Id, color));
    }

    /// <summary>
    ///     Toggles the privacy setting for a todo list.
    /// </summary>
    [SlashCommand("toggleprivacy", "Toggles the privacy setting for a todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task TogglePrivacy(
        [Summary("list-id", "The ID of the todo list")]
        int listId
    )
    {
        await DeferAsync();

        var newPrivacy = await Service.ToggleTodoListPrivacyAsync(listId, ctx.User.Id);

        if (newPrivacy is null)
        {
            await FollowupAsync(Strings.TodoTogglePrivacyFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var privacyText = newPrivacy.Value ? "public" : "private";
        await FollowupAsync(Strings.TodoPrivacyToggled(ctx.Guild.Id, privacyText));
    }

    /// <summary>
    ///     Shows all permissions for a todo list.
    /// </summary>
    [SlashCommand("permissions", "Shows all permissions for a todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task Permissions(
        [Summary("list-id", "The ID of the todo list")]
        int listId
    )
    {
        await DeferAsync();

        var permissions = await Service.GetTodoListPermissionsAsync(listId, ctx.User.Id);

        if (permissions is null)
        {
            await FollowupAsync(Strings.TodoPermissionsViewFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (permissions.Count == 0)
        {
            await FollowupAsync(Strings.TodoNoPermissionsFound(ctx.Guild.Id), ephemeral: true);
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

        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Revokes all permissions for a user on a todo list.
    /// </summary>
    [SlashCommand("revoke", "Revokes all permissions for a user on a todo list")]
    [RequireContext(ContextType.Guild)]
    public async Task Revoke(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("user", "The user to revoke permissions from")]
        IUser user
    )
    {
        await DeferAsync();

        var success = await Service.RevokeTodoListPermissionsAsync(listId, user.Id, ctx.User.Id);

        if (!success)
        {
            await FollowupAsync(Strings.TodoRevokeFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupAsync(Strings.TodoPermissionsRevoked(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Searches for todo items within a list.
    /// </summary>
    [SlashCommand("search", "Searches for todo items within a list")]
    [RequireContext(ContextType.Guild)]
    public async Task Search(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("query", "The search query")] string query
    )
    {
        await DeferAsync();

        var items = await Service.SearchTodoItemsAsync(listId, ctx.User.Id, query, false);

        if (items is null)
        {
            await FollowupAsync(Strings.TodoSearchFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (items.Count == 0)
        {
            await FollowupAsync(Strings.TodoSearchNoResults(ctx.Guild.Id, query), ephemeral: true);
            return;
        }

        var pages = new List<PageBuilder>();
        const int itemsPerPage = 8;

        for (var i = 0; i < items.Count; i += itemsPerPage)
        {
            var pageItems = items.Skip(i).Take(itemsPerPage);
            var sb = new StringBuilder();
            sb.AppendLine($"**{Strings.TodoSearchResults(ctx.Guild.Id, items.Count, query)}**");

            foreach (var item in pageItems)
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

            var page = new PageBuilder()
                .WithTitle(Strings.TodoSearchTitle(ctx.Guild.Id))
                .WithDescription(sb.ToString())
                .WithColor(Color.Orange)
                .WithFooter($"Search Page {(i / itemsPerPage) + 1}");

            pages.Add(page);
        }

        var paginator = new StaticPaginatorBuilder()
            .WithPages(pages)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithUsers(ctx.User)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteInput)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    ///     Filters todo items by tag or priority.
    /// </summary>
    [SlashCommand("filter", "Filters todo items by tag or priority")]
    [RequireContext(ContextType.Guild)]
    public async Task Filter(
        [Summary("list-id", "The ID of the todo list")]
        int listId,
        [Summary("type", "Filter type")] [Choice("Tag", "tag")] [Choice("Priority", "priority")]
        string filterType,
        [Summary("value", "The value to filter by")]
        string value
    )
    {
        await DeferAsync();

        List<TodoItem>? items = null;

        switch (filterType.ToLower())
        {
            case "tag":
                items = await Service.FilterTodoItemsAsync(listId, ctx.User.Id, value, includeCompleted: false);
                break;

            case "priority":
                if (!int.TryParse(value, out var priority) || priority < 1 || priority > 4)
                {
                    await FollowupAsync(Strings.TodoFilterPriorityInvalid(ctx.Guild.Id), ephemeral: true);
                    return;
                }

                items = await Service.FilterTodoItemsAsync(listId, ctx.User.Id, priority: priority,
                    includeCompleted: false);
                break;
        }

        if (items is null)
        {
            await FollowupAsync(Strings.TodoFilterFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (items.Count == 0)
        {
            await FollowupAsync(Strings.TodoFilterNoResults(ctx.Guild.Id, filterType, value), ephemeral: true);
            return;
        }

        var pages = new List<PageBuilder>();
        const int itemsPerPage = 8;

        for (var i = 0; i < items.Count; i += itemsPerPage)
        {
            var pageItems = items.Skip(i).Take(itemsPerPage);
            var sb = new StringBuilder();
            sb.AppendLine($"**{Strings.TodoFilterResults(ctx.Guild.Id, items.Count, filterType, value)}**");

            foreach (var item in pageItems)
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

            var page = new PageBuilder()
                .WithTitle(Strings.TodoFilterTitle(ctx.Guild.Id))
                .WithDescription(sb.ToString())
                .WithColor(Color.Gold)
                .WithFooter($"Filter Page {(i / itemsPerPage) + 1}");

            pages.Add(page);
        }

        var paginator = new StaticPaginatorBuilder()
            .WithPages(pages)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithUsers(ctx.User)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteInput)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5));
    }
}