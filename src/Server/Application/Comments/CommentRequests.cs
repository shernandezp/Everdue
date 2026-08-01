using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Comments;

public sealed record ListCommentsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<CommentDto>>;

/// <summary>
/// Mentions are **picked, not parsed**: the client sends the ids its picker chose and the body stays
/// plain text. Parsing "@name" out of free text would need display names to be unique, stable and
/// escapable — three things they are not — for a feature worth one notification.
/// </summary>
public sealed record AddCommentCommand(
    Guid WorkItemId,
    [property: Required, MaxLength(4000)] string Body,
    IReadOnlyList<Guid>? MentionedUserIds = null) : ICommand<CommentDto>;

/// <summary>Own comment, or any comment if you are an administrator. There is no edit — a comment is a record.</summary>
public sealed record DeleteCommentCommand(Guid Id) : ICommand<bool>;
