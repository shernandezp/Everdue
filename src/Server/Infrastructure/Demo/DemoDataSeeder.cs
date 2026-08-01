using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Demo;

/// <summary>
/// One command, and every screen has something on it.
///
/// This is the highest-leverage adoption feature in v2.5: an empty install shows nothing that makes Everdue
/// different — the ledger, the compliance strip and the health table are all invisible without history — and a
/// stranger's first ten minutes decide whether there is an eleventh.
///
/// <para><strong>Refuses a database that already holds work items.</strong> A seeder that can overwrite a real
/// install is a data-loss bug waiting for a typo in a compose file, so the check is on data rather than on the
/// environment name.</para>
/// </summary>
public sealed class DemoDataSeeder(
    EverdueDbContext db,
    UserManager<AppUser> userManager,
    ITenantProvider tenants,
    IClock clock,
    IOptions<DemoOptions> options,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>Fixed so two demo installs look the same and a screenshot stays true.</summary>
    private const int Seed = 20260729;

    /// <summary>
    /// The startup path. Both guards belong here and not in <see cref="SeedNowAsync"/>: the flag, because
    /// starting a container must never be what seeds a database, and the emptiness check, because a compose
    /// file pointed at the wrong volume is the accident this feature is one typo away from.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Seed)
        {
            return;
        }

        if (await db.WorkItems.AnyAsync(cancellationToken) || await db.Entities.AnyAsync(cancellationToken))
        {
            logger.LogWarning(
                "Demo:Seed is set but this database already contains data. Nothing was seeded — point Demo mode at an empty data directory.");

            return;
        }

        await SeedNowAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds unconditionally, and reports what it wrote.
    ///
    /// <para>No emptiness check, on purpose: the only caller that reaches this directly is the demo-mode
    /// command, which has just wiped the tenant itself and has an administrator's confirmation for having done
    /// so. Repeating the check here would make the one legitimate use of the seeder impossible.</para>
    /// </summary>
    public async Task<DemoSeedSummary> SeedNowAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await tenants.GetAsync(cancellationToken);

        // The invariant this feature rests on: DEMO DATA PRESENT IMPLIES THE FLAG IS SET. Marked here, by the
        // code that writes the data, rather than only by the command that calls it — otherwise the startup
        // path (Demo:Seed on an empty database) produces six months of invented history that the app insists
        // is real, and the badge every user relies on never appears. Set *before* the rows, because a seed
        // that fails half way has still put demo data in the database.
        //
        // ITenantProvider hands out a cached AsNoTracking instance, so the row has to be read again to write it.
        var row = await db.Tenants.FirstAsync(t => t.Id == tenant.Id, cancellationToken);
        row.DemoMode = true;
        await db.SaveChangesAsync(cancellationToken);
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);
        var now = clock.UtcNow;
        var today = TenantTime.LocalDate(now, timeZone);
        var from = today.AddMonths(-Math.Max(1, options.Value.Months));

        var random = new Random(Seed);

        var admin = await db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var members = await CreateUsersAsync(tenant.Id, now, cancellationToken);
        var owners = members.Select(m => m.Id).ToList();

        if (admin is not null)
        {
            owners.Add(admin.Id);
        }

        var departments = CreateDepartments();
        var entities = CreateEntities();
        var fields = CreateFieldDefinitions(entities);

        db.Departments.AddRange(departments);
        db.Entities.AddRange(entities);
        db.EntityFieldDefs.AddRange(fields);
        await db.SaveChangesAsync(cancellationToken);

        var responsibilities = CreateResponsibilities(departments, entities, owners, from, random);

        db.Responsibilities.AddRange(responsibilities.Select(r => r.Responsibility));
        db.ChecklistTemplateItems.AddRange(responsibilities.SelectMany(r => r.Template));
        await db.SaveChangesAsync(cancellationToken);

        var builder = new DemoLedgerBuilder(random);
        var items = 0;

        foreach (var (responsibility, template, behaviour) in responsibilities)
        {
            var ledger = builder.Build(responsibility, template, behaviour, owners, timeZone, from, now);

            db.WorkItems.AddRange(ledger.Items);
            db.WorkItemEvents.AddRange(ledger.Events);
            db.ChecklistItems.AddRange(ledger.Checklist);

            items += ledger.Items.Count;

            // Saved per responsibility rather than all at once: a six-month daily schedule is a few hundred rows,
            // and twelve of those in one change tracker is a lot of memory for no benefit.
            await db.SaveChangesAsync(cancellationToken);
        }

        var tasks = CreateOneOffTasks(entities, departments, owners, timeZone, today, random);
        db.WorkItems.AddRange(tasks.Items);
        db.WorkItemEvents.AddRange(tasks.Events);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Demo data seeded: {Users} users (password '{Password}'), {Entities} entities, {Responsibilities} responsibilities, " +
            "{Occurrences} occurrences and {Tasks} one-off tasks over {Months} months. This install is a DEMO — do not put real work in it.",
            members.Count,
            options.Value.Password,
            entities.Count,
            responsibilities.Count,
            items,
            tasks.Items.Count,
            options.Value.Months);

        return new DemoSeedSummary(
            members.Count,
            entities.Count,
            responsibilities.Count,
            items,
            tasks.Items.Count,
            options.Value.Password);
    }

    private async Task<List<AppUser>> CreateUsersAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (string Email, string Name, UserRole Role, string Language)[] people =
        [
            ("ana@demo.everdue.app", "Ana Restrepo", UserRole.Admin, Languages.Spanish),
            ("carlos@demo.everdue.app", "Carlos Méndez", UserRole.Member, Languages.Spanish),
            ("diana@demo.everdue.app", "Diana Ospina", UserRole.Member, Languages.Spanish),
            ("john@demo.everdue.app", "John Baker", UserRole.Member, Languages.English),
            ("luisa@demo.everdue.app", "Luisa Franco", UserRole.Member, Languages.Spanish),
            ("marco@demo.everdue.app", "Marco Ricci", UserRole.Member, Languages.English),
        ];

        var created = new List<AppUser>();

        foreach (var (email, name, role, language) in people)
        {
            var user = new AppUser
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = name,
                Role = role,
                PreferredLanguage = language,
                Active = true,

                // Demo accounts are meant to be signed into immediately; a forced change would make the
                // documented one-command walkthrough six commands.
                MustChangePassword = false,
                CreatedAt = now,
            };

            var result = await userManager.CreateAsync(user, options.Value.Password);

            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Could not create demo user {Email}: {Errors}",
                    email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));

                continue;
            }

            created.Add(user);
        }

        _ = cancellationToken;
        return created;
    }

    private static List<Department> CreateDepartments() =>
    [
        new() { Id = Guid.CreateVersion7(), Name = "Operations", Active = true },
        new() { Id = Guid.CreateVersion7(), Name = "Administration", Active = true },
        new() { Id = Guid.CreateVersion7(), Name = "Maintenance", Active = true },
    ];

    private static List<Entity> CreateEntities() =>
    [
        New("Acme Distribución", EntityType.Customer),
        New("Comercial Ríos", EntityType.Customer),
        New("Ferretería El Progreso", EntityType.Customer),
        New("Hotel Miramar", EntityType.Customer),
        New("Talleres Vega", EntityType.Customer),
        New("Suministros Andinos", EntityType.Supplier),
        New("Empaques del Norte", EntityType.Supplier),
        New("Forklift #2", EntityType.Equipment),
        New("Delivery van ABC-123", EntityType.Equipment),
        New("Cold room", EntityType.Equipment),
        New("Head office", EntityType.Company),
    ];

    private static Entity New(string name, EntityType type)
        => new() { Id = Guid.CreateVersion7(), Name = name, Type = type, Active = true };

    /// <summary>
    /// Two definitions, so the feature is visible without pretending an entity is a customer record: one
    /// reference on a customer, one on a machine. Exactly what the guardrails allow and no more.
    /// </summary>
    private static List<EntityFieldDef> CreateFieldDefinitions(List<Entity> entities)
    {
        var accountManager = new EntityFieldDef
        {
            Id = Guid.CreateVersion7(),
            EntityType = EntityType.Customer,
            Name = "Account manager",
            FieldType = EntityFieldType.Text,
            Position = 0,
            Active = true,
        };

        var serial = new EntityFieldDef
        {
            Id = Guid.CreateVersion7(),
            EntityType = EntityType.Equipment,
            Name = "Serial no.",
            FieldType = EntityFieldType.Text,
            Position = 0,
            Active = true,
        };

        string[] managers = ["Ana Restrepo", "Carlos Méndez", "Diana Ospina", "Luisa Franco"];
        var index = 0;

        foreach (var entity in entities.Where(e => e.Type == EntityType.Customer))
        {
            entity.CustomFieldsJson = EntityCustomFields.Serialize(
                new Dictionary<Guid, string> { [accountManager.Id] = managers[index++ % managers.Length] });
        }

        var serialIndex = 1;

        foreach (var entity in entities.Where(e => e.Type == EntityType.Equipment))
        {
            entity.CustomFieldsJson = EntityCustomFields.Serialize(
                new Dictionary<Guid, string> { [serial.Id] = $"SN-2024-{serialIndex++:000}" });
        }

        return [accountManager, serial];
    }

    private sealed record DemoResponsibility(
        Responsibility Responsibility,
        List<ChecklistTemplateItem> Template,
        DemoBehaviour Behaviour);

    /// <summary>
    /// Twelve responsibilities covering every recurrence kind, two with checklists, one requiring photo proof,
    /// and one deliberately chronic so the "what do I fix" screens are not empty.
    /// </summary>
    private static List<DemoResponsibility> CreateResponsibilities(
        List<Department> departments,
        List<Entity> entities,
        List<Guid> owners,
        DateOnly from,
        Random random)
    {
        var operations = departments[0];
        var administration = departments[1];
        var maintenance = departments[2];

        var customers = entities.Where(e => e.Type == EntityType.Customer).ToList();
        var suppliers = entities.Where(e => e.Type == EntityType.Supplier).ToList();
        var equipment = entities.Where(e => e.Type == EntityType.Equipment).ToList();

        var list = new List<DemoResponsibility>();

        void Add(
            string title,
            RecurrenceKind kind,
            Department department,
            Entity? entity,
            DemoBehaviour behaviour,
            int? daysMask = null,
            int? dayOfMonth = null,
            int? monthOfYear = null,
            List<(string Text, bool Required)>? checklist = null,
            bool requirePhoto = false)
        {
            var responsibility = new Responsibility
            {
                Id = Guid.CreateVersion7(),
                Title = title,
                OwnerUserId = owners[random.Next(owners.Count)],
                DepartmentId = department.Id,
                EntityId = entity?.Id,
                RecurrenceKind = kind,
                DaysOfWeekMask = kind == RecurrenceKind.WeeklyOnDays ? daysMask : null,
                DayOfMonth = kind is RecurrenceKind.MonthlyOnDay or RecurrenceKind.Yearly ? dayOfMonth : null,
                MonthOfYear = kind == RecurrenceKind.Yearly ? monthOfYear : null,
                StartDate = from,
                Active = true,
                RequireChecklistToComplete = checklist is { Count: > 0 },
                RequireAttachmentToComplete = requirePhoto,
            };

            var template = (checklist ?? [])
                .Select((line, index) => new ChecklistTemplateItem
                {
                    Id = Guid.CreateVersion7(),
                    ResponsibilityId = responsibility.Id,
                    Text = line.Text,
                    Required = line.Required,
                    Position = index,
                })
                .ToList();

            list.Add(new DemoResponsibility(responsibility, template, behaviour));
        }

        Add("Weekly follow-up call", RecurrenceKind.WeeklyOnDays, operations, customers[0], DemoBehaviour.Reliable,
            daysMask: RecurrenceRule.MaskFor(DayOfWeek.Monday));

        Add("Weekly follow-up call", RecurrenceKind.WeeklyOnDays, operations, customers[1], DemoBehaviour.Patchy,
            daysMask: RecurrenceRule.MaskFor(DayOfWeek.Tuesday));

        Add("Weekly follow-up call", RecurrenceKind.WeeklyOnDays, operations, customers[2], DemoBehaviour.Chronic,
            daysMask: RecurrenceRule.MaskFor(DayOfWeek.Wednesday));

        Add("Confirm next delivery date", RecurrenceKind.WeeklyOnDays, operations, customers[3], DemoBehaviour.Reliable,
            daysMask: RecurrenceRule.MaskFor(DayOfWeek.Thursday));

        Add("Chase open purchase orders", RecurrenceKind.WeeklyOnDays, administration, suppliers[0], DemoBehaviour.Patchy,
            daysMask: RecurrenceRule.MaskFor(DayOfWeek.Friday));

        Add("Daily cash count", RecurrenceKind.Daily, administration, null, DemoBehaviour.Reliable);

        Add("Cold room temperature log", RecurrenceKind.Daily, maintenance, equipment[2], DemoBehaviour.Reliable,
            checklist:
            [
                ("Read the morning temperature", true),
                ("Read the evening temperature", true),
                ("Check the door seal", false),
            ]);

        Add("Forklift safety inspection", RecurrenceKind.WeeklyOnDays, maintenance, equipment[0], DemoBehaviour.Patchy,
            daysMask: RecurrenceRule.MaskFor(DayOfWeek.Monday, DayOfWeek.Thursday),
            checklist:
            [
                ("Tyres and forks undamaged", true),
                ("Horn and lights working", true),
                ("Hydraulic fluid level", true),
                ("Log book signed", false),
            ],
            requirePhoto: true);

        Add("Delivery van pre-trip check", RecurrenceKind.Daily, maintenance, equipment[1], DemoBehaviour.Patchy);

        Add("Monthly inventory check", RecurrenceKind.MonthlyOnDay, operations, null, DemoBehaviour.Reliable,
            dayOfMonth: 1);

        Add("Supplier price review", RecurrenceKind.MonthlyOnDay, administration, suppliers[1], DemoBehaviour.Patchy,
            dayOfMonth: 15);

        Add("Fire extinguisher certification", RecurrenceKind.Yearly, maintenance, null, DemoBehaviour.Reliable,
            dayOfMonth: 15,
            monthOfYear: 3);

        return list;
    }

    private sealed record DemoTasks(List<WorkItem> Items, List<WorkItemEvent> Events);

    /// <summary>
    /// One-off work, so the board has a mix and the concentration report can split occurrences from tasks. A
    /// team's day is not only recurring obligations.
    /// </summary>
    private static DemoTasks CreateOneOffTasks(
        List<Entity> entities,
        List<Department> departments,
        List<Guid> owners,
        TimeZoneInfo timeZone,
        DateOnly today,
        Random random)
    {
        (string Title, int DayOffset)[] work =
        [
            ("Send quotation for the new shelving", -21),
            ("Resolve the invoice discrepancy", -14),
            ("Book the annual electrical inspection", -9),
            ("Update the delivery route sheet", -7),
            ("Return the damaged pallet jack", -5),
            ("Collect signed delivery notes", -3),
            ("Prepare the month-end summary", -1),
            ("Call about the late shipment", 0),
            ("Check the new supplier's references", 1),
            ("Reprint the warehouse labels", 2),
            ("Arrange cover for Friday's route", 4),
            ("Order replacement safety vests", 6),
            ("Follow up on the credit note", 9),
            ("Schedule the forklift service", 12),
            ("Review the packaging quotes", 15),
        ];

        var items = new List<WorkItem>();
        var events = new List<WorkItemEvent>();

        foreach (var (title, offset) in work)
        {
            var due = TenantTime.EndOfDay(today.AddDays(offset), timeZone);
            var created = due.AddDays(-random.Next(1, 6));
            var entity = random.Next(100) < 70 ? entities[random.Next(entities.Count)] : null;

            var item = new WorkItem
            {
                Id = Guid.CreateVersion7(),
                ResponsibilityId = null,
                Title = title,
                OwnerUserId = owners[random.Next(owners.Count)],
                EntityId = entity?.Id,
                DepartmentId = departments[random.Next(departments.Count)].Id,
                DueDate = due,
                CreatedAt = created,
                Status = WorkItemStatus.Open,
            };

            items.Add(item);
            events.Add(WorkItemEventFactory.Created(item, item.OwnerUserId, created, new { source = WorkItemSources.OneOff, demo = true }));

            // Past-due tasks are mostly done; a couple are left overdue, because that is what a real board looks
            // like and it is what the exception dashboard exists to surface.
            if (offset < 0 && random.Next(100) < 75)
            {
                var completedAt = due.AddHours(-random.Next(1, 30));

                item.Status = WorkItemStatus.Completed;
                item.CompletedAt = completedAt;
                item.CompletedByUserId = item.OwnerUserId;

                events.Add(WorkItemEventFactory.StatusChanged(
                    item,
                    item.OwnerUserId,
                    completedAt,
                    WorkItemStatus.Open,
                    WorkItemStatus.Completed,
                    null));
            }
        }

        return new DemoTasks(items, events);
    }
}
