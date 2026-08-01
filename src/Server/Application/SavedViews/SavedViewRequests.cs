using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.SavedViews;

public sealed record ListSavedViewsQuery : IQuery<IReadOnlyList<SavedViewDto>>;

/// <summary>
/// Saving the same name twice replaces it rather than failing: "save this view" is a gesture people
/// repeat as they refine a filter set, and refusing the second one would be pedantry.
/// </summary>
public sealed record SaveSavedViewCommand(string Name, string Route, string QueryString) : ICommand<SavedViewDto>;

public sealed record DeleteSavedViewCommand(Guid Id) : ICommand<bool>;
