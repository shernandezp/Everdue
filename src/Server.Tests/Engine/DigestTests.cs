using Everdue.Server.Domain;
using Everdue.Server.Engine.Digest;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Tests.Engine;

/// <summary>
/// The digest is the only user-facing text the server renders, and the only part of the
/// localization guarantee the i18n check cannot see — so it is asserted in both languages here.
/// </summary>
public class DigestTests
{
    private static DateOnly D(string value) => DateOnly.Parse(value);

    private static DigestContent Sample() => new(
        "Acme Operations",
        D("2026-07-28"),
        TimeZoneLookup.Resolve("America/Bogota"),
        DigestFrequency.Daily,
        DepartmentName: null,
        WentMissed: [new DigestItem("Weekly follow-up", "Globex", "María", DateTimeOffset.Parse("2026-07-27T04:59:59Z"))],
        DueToday: [new DigestItem("Inventory check", null, "John", DateTimeOffset.Parse("2026-07-29T04:59:59Z"))],
        OnHold: [new DigestHoldGroup(HoldReason.WaitingCustomer, 3), new DigestHoldGroup(HoldReason.Other, 1)],
        OnHoldAging: [new DigestAgingRow("Globex", HoldReason.WaitingCustomer, 2, 12)],
        Neglect: [new DigestNeglectRow("Initech", 45, 3)]);

    [Theory]
    [InlineData(Languages.Spanish, "Incumplidas desde ayer", "Vencen hoy", "En espera", "Esperando cliente")]
    [InlineData(Languages.English, "Went missed since yesterday", "Due today", "On hold", "Waiting on customer")]
    public void The_digest_renders_every_section_in_both_languages(
        string language,
        string missedHeading,
        string dueHeading,
        string holdHeading,
        string reason)
    {
        var html = DigestTemplates.RenderHtml(Sample(), language);

        html.ShouldContain(missedHeading);
        html.ShouldContain(dueHeading);
        html.ShouldContain(holdHeading);
        html.ShouldContain(reason);

        html.ShouldContain("Acme Operations");
        html.ShouldContain("Weekly follow-up");
        html.ShouldContain("Inventory check");
        html.ShouldContain("Globex");

        // Counts appear next to their headings.
        html.ShouldContain($"{missedHeading} (1)");
        html.ShouldContain($"{holdHeading} (4)");

        DigestTemplates.Subject(Sample(), language).ShouldContain("Everdue");
    }

    [Fact]
    public void An_unknown_language_falls_back_rather_than_rendering_raw_keys()
    {
        var html = DigestTemplates.RenderHtml(Sample(), "fr");

        html.ShouldNotContain("wentMissed");
        html.ShouldNotContain("reason.");
        html.ShouldContain("Incumplidas desde ayer"); // the tenant-default language
    }

    [Fact]
    public void Empty_sections_say_so_instead_of_rendering_an_empty_table()
    {
        var empty = new DigestContent("Acme", D("2026-07-28"), TimeZoneInfo.Utc, DigestFrequency.Daily, null, [], [], [], [], []);
        empty.IsEmpty.ShouldBeTrue();

        DigestTemplates.RenderHtml(empty, Languages.English).ShouldContain("Nothing to report.");
    }

    [Fact]
    public void Text_from_the_database_is_html_encoded()
    {
        var content = Sample() with
        {
            TenantName = "Acme <script>alert(1)</script>",
            DueToday = [new DigestItem("Call \"Bob\" & co", "R&D", "O'Brien", DateTimeOffset.Parse("2026-07-29T04:59:59Z"))],
        };

        var html = DigestTemplates.RenderHtml(content, Languages.English);

        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
        html.ShouldContain("R&amp;D");
    }

    /// <summary>
    /// The three sections are read from the ledger, not hand-assembled: "went missed" is anchored to
    /// the period boundary so the digest says the same thing whether the engine flipped the row at
    /// midnight or caught up at 06:55.
    /// </summary>
    [Fact]
    public async Task The_builder_reads_the_three_sections_from_the_ledger()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T13:00:00Z"); // 08:00 in Bogota

        var entity = harness.AddEntity("Globex", EntityType.Supplier);
        var now = harness.Clock.UtcNow;
        var timeZone = harness.TimeZone;
        var today = TenantTime.LocalDate(now, timeZone);

        harness.Db.WorkItems.AddRange(
            // Went missed six hours ago: its period ended inside the last 24h.
            Occurrence("Missed overnight", WorkItemStatus.Missed, now.AddHours(-6), entity.Id, harness),
            // Missed last week — already reported, must not repeat.
            Occurrence("Missed last week", WorkItemStatus.Missed, now.AddDays(-8), entity.Id, harness),
            // Due today, still open.
            OneOff("Due today", WorkItemStatus.Open, TenantTime.EndOfDay(today, timeZone), entity.Id, harness),
            // Due tomorrow: not today's problem.
            OneOff("Due tomorrow", WorkItemStatus.Open, TenantTime.EndOfDay(today.AddDays(1), timeZone), entity.Id, harness),
            // On hold, counted by reason.
            Hold(OneOff("Blocked", WorkItemStatus.OnHold, TenantTime.EndOfDay(today, timeZone), entity.Id, harness), HoldReason.WaitingSupplier));

        await harness.Db.SaveChangesAsync();

        var content = await new DigestBuilder(harness.Db, new UserDirectory(harness.Db), new EmptyReportSender())
            .BuildAsync(harness.Tenant, now, DigestFrequency.Daily, departmentId: null, CancellationToken.None);

        content.WentMissed.Select(i => i.Title).ShouldBe(["Missed overnight"]);

        // "Due today" is what is expected today and not yet done, which includes blocked work: it is
        // still due, and the hold section below says why. Anything due tomorrow stays out.
        content.DueToday.Select(i => i.Title).ShouldBe(["Due today", "Blocked"], ignoreOrder: true);
        content.DueToday.ShouldNotContain(i => i.Title == "Due tomorrow");

        content.OnHold.Single().Reason.ShouldBe(HoldReason.WaitingSupplier);
        content.OnHold.Single().Count.ShouldBe(1);

        content.WentMissed.Single().OwnerName.ShouldBe("Owner");
        content.WentMissed.Single().EntityName.ShouldBe("Globex");
    }

    private static WorkItem Occurrence(string title, WorkItemStatus status, DateTimeOffset periodEnd, Guid entityId, EngineHarness harness)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = harness.Tenant.Id,
            ResponsibilityId = null,
            Title = title,
            OwnerUserId = harness.Owner.Id,
            EntityId = entityId,
            PeriodStart = periodEnd.AddDays(-7),
            PeriodEnd = periodEnd,
            DueDate = periodEnd.AddSeconds(-1),
            Status = status,
            CreatedAt = periodEnd.AddDays(-7),
        };

    private static WorkItem OneOff(string title, WorkItemStatus status, DateTimeOffset due, Guid entityId, EngineHarness harness)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = harness.Tenant.Id,
            Title = title,
            OwnerUserId = harness.Owner.Id,
            EntityId = entityId,
            DueDate = due,
            Status = status,
            CreatedAt = due.AddDays(-1),
        };

    private static WorkItem Hold(WorkItem item, HoldReason reason)
    {
        item.HoldReason = reason;
        return item;
    }
}
