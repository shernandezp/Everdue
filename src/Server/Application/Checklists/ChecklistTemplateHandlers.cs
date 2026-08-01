using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Checklists;

internal static class ChecklistTemplateMapping
{
    public static ChecklistTemplateItemDto ToDto(ChecklistTemplateItem item)
        => new(item.Id, item.Text, item.Required, item.Position);
}

public sealed class GetChecklistTemplateHandler(IEverdueDbContext db)
    : IRequestHandler<GetChecklistTemplateQuery, IReadOnlyList<ChecklistTemplateItemDto>>
{
    public async Task<IReadOnlyList<ChecklistTemplateItemDto>> Handle(
        GetChecklistTemplateQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Responsibilities.AnyAsync(r => r.Id == request.ResponsibilityId, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Responsibility, request.ResponsibilityId);
        }

        var items = await db.ChecklistTemplateItems.AsNoTracking()
            .Where(t => t.ResponsibilityId == request.ResponsibilityId)
            .OrderBy(t => t.Position)
            .ToListAsync(cancellationToken);

        return items.Select(ChecklistTemplateMapping.ToDto).ToArray();
    }
}

/// <summary>
/// Replaces a responsibility's template.
///
/// Existing occurrences are <strong>not</strong> touched: their checklist is a snapshot taken at spawn,
/// which is the whole reason it is a copy. Only occurrences the engine has yet to create pick this up.
/// </summary>
public sealed class SaveChecklistTemplateHandler(IEverdueDbContext db, IOptions<ChecklistOptions> options)
    : IRequestHandler<SaveChecklistTemplateCommand, IReadOnlyList<ChecklistTemplateItemDto>>
{
    public async Task<IReadOnlyList<ChecklistTemplateItemDto>> Handle(
        SaveChecklistTemplateCommand request,
        CancellationToken cancellationToken = default)
    {
        var responsibility = await db.Responsibilities.AsNoTracking()
                                .FirstOrDefaultAsync(r => r.Id == request.ResponsibilityId, cancellationToken)
                            ?? throw new NotFoundException(ResourceNames.Responsibility, request.ResponsibilityId);

        var max = options.Value.MaxItemsPerTemplate;

        if (request.Items.Count > max)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["items"] = [$"A checklist template may hold at most {max} items."],
            });
        }

        var texts = request.Items
            .Select(item => item.Text.Trim())
            .Where(text => text.Length > 0)
            .ToArray();

        if (texts.Length != request.Items.Count)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["items"] = ["A checklist item cannot be blank."],
            });
        }

        var existing = await db.ChecklistTemplateItems
            .Where(t => t.ResponsibilityId == responsibility.Id)
            .ToListAsync(cancellationToken);

        db.ChecklistTemplateItems.RemoveRange(existing);

        var replacement = request.Items
            .Select((item, index) => new ChecklistTemplateItem
            {
                Id = Guid.CreateVersion7(),
                ResponsibilityId = responsibility.Id,
                Text = item.Text.Trim(),
                Required = item.Required,
                Position = index,
            })
            .ToArray();

        db.ChecklistTemplateItems.AddRange(replacement);
        await db.SaveChangesAsync(cancellationToken);

        return replacement.Select(ChecklistTemplateMapping.ToDto).ToArray();
    }
}
