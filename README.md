# Nexus SDK

The official .NET client for the [Nexus](https://github.com/sassy-solutions) platform, in two
packages.

| Package | What it is for |
|---|---|
| **`SassySolutions.Nexus.Sdk`** | What an application takes to be **governed by** Nexus: remote configuration, feature flags, usage and billing, permission checks, health, startup registration. |
| **`SassySolutions.Nexus.Management`** | What you take to **build an administration console**: organizations, projects, applications, environments, roles, git connections, platform resources. |

```bash
dotnet add package SassySolutions.Nexus.Sdk
```

```csharp
builder.Services.AddNexus(builder.Configuration);
```

That is the whole setup. `Nexus:BaseUrl` and `Nexus:ApiKey` come from your configuration.

## The client package has no administration surface

Not "does not use", **does not name**. A contract test fails the build if the client package's
route registry ever contains a route under `admin/` or `platform/`, or any route the governance
package owns.

This is not tidiness. Those routes are gated by administrator policies and refuse an API-key
principal outright — an API key carries scopes, not roles. An application that has no
administrator token could only ever be refused by them, so it should not be able to promise them.

## Source-available, not open source

You may read this code, audit it, and review it for security. Using it requires a Nexus licence;
redistributing it on its own does not become permitted by reading it. Shipping it **inside your
own application** is explicitly allowed — see section 3(b) of [LICENSE](LICENSE), which exists
precisely so that deploying your app is not a breach.

## Building

```bash
dotnet restore   # no credentials needed: every dependency is public on nuget.org
dotnet build
dotnet test
```

The two packages move in **lockstep**, and the dependency between them is an exact version
range. A mismatched pair is not degraded — it is silently wrong on the wire.

## Licence

Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
Nexus is commercialised under the SASSY SOLUTIONS trade name.

Questions: legal@scojhconsult.com
