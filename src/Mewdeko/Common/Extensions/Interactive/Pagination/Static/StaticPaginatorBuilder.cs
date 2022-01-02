using System.Collections.Generic;
using System.Collections.ObjectModel;
using Discord;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;

namespace Mewdeko.Common.Extensions.Interactive.Pagination.Static;

/// <summary>
///     Represents a builder class for making a <see cref="StaticPaginator" />.
/// </summary>
public sealed class StaticPaginatorBuilder : PaginatorBuilder<StaticPaginator, StaticPaginatorBuilder>
{
    /// <summary>
    ///     Gets or sets the pages of the <see cref="Paginator" />.
    /// </summary>
    public IList<PageBuilder> Pages { get; set; }

    public override StaticPaginator Build(int startPageIndex = 0)
    {
        if (Options.Count == 0) WithDefaultEmotes();

        if (Pages != null)
            for (var i = 0; i < Pages.Count; i++)
                Pages[i].WithPaginatorFooter(Footer, i, Pages.Count - 1, Users);

        return new StaticPaginator(
            Users?.ToArray() ?? Array.Empty<IUser>(),
            new ReadOnlyDictionary<IEmote, PaginatorAction>(
                Options),
            CanceledPage?.Build(),
            TimeoutPage?.Build(),
            Deletion,
            InputType,
            ActionOnCancellation,
            ActionOnTimeout,
            Pages?.Select(x => x.Build()).ToArray(),
            startPageIndex);
    }

    /// <summary>
    ///     Sets the pages of the paginator.
    /// </summary>
    public StaticPaginatorBuilder WithPages(params PageBuilder[] pages)
    {
        Pages = pages.ToList();
        return this;
    }

    /// <summary>
    ///     Sets the pages of the paginator.
    /// </summary>
    public StaticPaginatorBuilder WithPages(IEnumerable<PageBuilder> pages)
    {
        Pages = pages.ToList();
        return this;
    }

    /// <summary>
    ///     Adds a page to the paginator.
    /// </summary>
    public StaticPaginatorBuilder AddPage(PageBuilder page)
    {
        Pages ??= new List<PageBuilder>();
        Pages.Add(page);
        return this;
    }
}