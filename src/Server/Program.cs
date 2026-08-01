// Everdue — operational accountability for small and medium businesses.
// Copyright (C) 2026 Everdue contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU
// Affero General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
// even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Affero General Public License for more details: <https://www.gnu.org/licenses/>.
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// The notice the AGPL actually requires of a *running* program (§5d) is served by GET /api/v1/about and
// rendered in the app's footer, so a user of a network instance can find the licence and the source.
// See Api/Endpoints/MetaEndpoints.cs.

using Everdue.Server.Api;
using Everdue.Server.Application;
using Everdue.Server.Engine;
using Everdue.Server.Hosting;
using Everdue.Server.Infrastructure;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,

    // Anchored to the binary, not to the working directory. A Windows service starts in
    // System32 and a systemd unit inherits whatever it is given; either way the SPA in wwwroot
    // and appsettings.json must still be found. Honours an explicit --contentRoot when one is
    // given (the integration tests rely on that).
    ContentRootPath = EverdueContentRoot.Resolve(args),
});

builder.Configuration.AddEverdueConfiguration(args);

// One process is the whole system: API, SPA host, occurrence engine and digest.
builder.Host.UseEverdueServiceHosting();

builder.Services.AddEverdueInfrastructure(builder.Configuration);
builder.Services.AddEverdueApplication();
builder.Services.AddEverdueApi(builder.Configuration);
builder.Services.AddEverdueEngine();

var app = builder.Build();

await app.InitializeEverdueDatabaseAsync();

app.UseEverduePipeline(builder.Configuration);
app.MapEverdueApi();
app.MapEverdueClient();

app.Run();

/// <summary>Exposed so the integration tests can spin the real host up with WebApplicationFactory.</summary>
public partial class Program;
