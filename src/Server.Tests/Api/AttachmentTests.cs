using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Acceptance criterion 14: the limits are real, the download is authenticated, and the uploaded
/// filename never reaches the filesystem.
/// </summary>
public class AttachmentTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static MultipartFormDataContent File(string name, string contentType, int sizeBytes = 16)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('x', sizeBytes)));
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { content, "file", name } };
    }

    private static async Task<Guid> ATaskAsync(EverdueApp app, HttpClient client)
    {
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var task = await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Delivery note",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        return task.Id;
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_file_uploads_lists_and_downloads(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        var response = await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File("note.pdf", "application/pdf"));
        await response.ShouldBeSuccessAsync();

        var listed = await admin.GetJsonAsync<IReadOnlyList<AttachmentDto>>($"/api/v1/workitems/{id}/attachments");
        var attachment = listed.Single();

        attachment.FileName.ShouldBe("note.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.UploadedByDisplayName.ShouldBe("Administrator");

        var download = await admin.GetAsync($"/api/v1/attachments/{attachment.Id}");
        await download.ShouldBeSuccessAsync();

        // Never rendered in this origin, and never cached.
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        download.Headers.CacheControl!.ToString().ShouldContain("no-store");

        // The bytes are named by id on disk; the uploaded name lives only in the column.
        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var row = await db.Attachments.SingleAsync();

            row.StorageKey.ShouldEndWith(row.Id.ToString());
            row.StorageKey.ShouldNotContain("note.pdf");
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_oversize_file_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Attachments:MaxSizeBytes"] = "2048" });

        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        var response = await admin.PostAsync(
            $"/api/v1/workitems/{id}/attachments",
            File("big.pdf", "application/pdf", sizeBytes: 4096));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).ShouldBe("validation_failed");
    }

    /// <summary>The declared type and the extension are both caller-controlled, so both have to agree.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_disallowed_type_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        var response = await admin.PostAsync(
            $"/api/v1/workitems/{id}/attachments",
            File("payload.exe", "application/octet-stream"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A PDF extension over a disallowed declared type is refused too.
        var mismatched = await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File("note.pdf", "application/octet-stream"));
        mismatched.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_per_item_cap_is_enforced(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Attachments:MaxPerWorkItem"] = "2" });

        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        for (var i = 0; i < 2; i++)
        {
            await (await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File($"note{i}.pdf", "application/pdf")))
                .ShouldBeSuccessAsync();
        }

        var third = await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File("note3.pdf", "application/pdf"));
        third.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A filename that tries to walk out of the store cannot: the key is built, never accepted.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_traversing_filename_cannot_escape_the_store(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        await (await admin.PostAsync(
                $"/api/v1/workitems/{id}/attachments",
                File("../../escaped.txt", "text/plain")))
            .ShouldBeSuccessAsync();

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var row = await db.Attachments.SingleAsync();

            row.StorageKey.ShouldNotContain("..");
            row.FileName.ShouldBe("escaped.txt"); // stripped to its leaf, and only used in the header
        });
    }

    /// <summary>
    /// A product shipped in Spanish meets "Guía de recepción.pdf" on its first day, and the plain
    /// <c>filename</c> parameter cannot carry it. Both forms go out: ASCII for old clients, and the
    /// RFC 5987 one that is actually right.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_non_ascii_filename_survives_the_download_header(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        await (await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File("Guía de recepción.txt", "text/plain")))
            .ShouldBeSuccessAsync();

        var attachment = (await admin.GetJsonAsync<IReadOnlyList<AttachmentDto>>($"/api/v1/workitems/{id}/attachments")).Single();
        attachment.FileName.ShouldBe("Guía de recepción.txt");

        var download = await admin.GetAsync($"/api/v1/attachments/{attachment.Id}");
        var header = download.Content.Headers.GetValues("Content-Disposition").Single();

        header.ShouldStartWith("attachment;");
        header.ShouldContain("filename*=UTF-8''");
        header.ShouldContain(Uri.EscapeDataString("Guía de recepción.txt"));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Downloading_requires_authentication(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var id = await ATaskAsync(app, admin);

        await (await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File("note.pdf", "application/pdf")))
            .ShouldBeSuccessAsync();

        var attachment = (await admin.GetJsonAsync<IReadOnlyList<AttachmentDto>>($"/api/v1/workitems/{id}/attachments")).Single();

        var anonymous = app.NewClient();
        (await anonymous.GetAsync($"/api/v1/attachments/{attachment.Id}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Deleting is the uploader's or an administrator's call — not everybody's.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Only_the_uploader_or_an_administrator_can_delete(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();
        var id = await ATaskAsync(app, admin);

        await (await admin.PostAsync($"/api/v1/workitems/{id}/attachments", File("note.pdf", "application/pdf")))
            .ShouldBeSuccessAsync();

        var attachment = (await admin.GetJsonAsync<IReadOnlyList<AttachmentDto>>($"/api/v1/workitems/{id}/attachments")).Single();

        (await member.DeleteAsync($"/api/v1/attachments/{attachment.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.DeleteAsync($"/api/v1/attachments/{attachment.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.GetJsonAsync<IReadOnlyList<AttachmentDto>>($"/api/v1/workitems/{id}/attachments")).ShouldBeEmpty();
    }
}
