using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Demo;

public sealed record GetDemoStatusQuery : IQuery<DemoStatusDto>;

/// <summary>
/// Turns demo mode on or off. <strong>Both directions wipe the tenant.</strong> On means "destroy everything,
/// then write six months of invented history"; off means "destroy everything, and leave an empty install for
/// real use". There is no third behaviour, and no way back from either.
///
/// <para>Hence two independent confirmations, neither of which a script can supply by accident:</para>
/// <list type="bullet">
/// <item><description><paramref name="Confirmation"/> must be the tenant's own name, typed out. This is the
/// gate against the wrong window: nobody types their company's name into a box by mistake.</description></item>
/// <item><description><paramref name="Password"/> must be the caller's own. This is the gate against the
/// unattended laptop: an admin cookie is not proof that the admin is at the keyboard.</description></item>
/// </list>
/// </summary>
public sealed record DemoModeCommand(
    bool Enabled,

    /// <summary>Must equal the tenant's name exactly, after trimming.</summary>
    [property: Required] string Confirmation,

    /// <summary>The caller's own password, re-entered.</summary>
    [property: Required] string Password) : ICommand<DemoModeResultDto>;
