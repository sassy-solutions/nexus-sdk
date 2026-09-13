# SassySolutions.Nexus.Management

The governance surface of the Nexus platform: organizations, projects, applications,
environments, roles, git connections, GitHub credentials and platform resources — everything
needed to build a Nexus administration console.

```csharp
builder.Services.AddNexusManagement(builder.Configuration, o =>
    o.BearerTokenProvider = ct => GetAdministratorTokenAsync(ct));
```

## This is not the package a tenant application needs

If you are building an application that runs **on** Nexus — reading configuration, gating on
feature flags, reporting usage — you want **`SassySolutions.Nexus.Sdk`**, not this one. It is a
dependency of this package, so taking this one gives you both.

These routes are gated by administrator policies and refuse an API-key principal outright: an
API key carries scopes, not roles. Without a `BearerTokenProvider`, every call here is refused.

## Versioning

This package and `SassySolutions.Nexus.Sdk` move in lockstep, and the dependency between them is
an **exact** version range. A mismatched pair is not degraded — it is silently wrong on the wire,
because both halves share the same options, the same handler chain and the same result types.

## Licence

Source-available, not open source. Reading it is free; using it requires a Nexus licence.
See [LICENSE](https://github.com/sassy-solutions/nexus-sdk/blob/main/LICENSE).

Copyright (c) 2024-2026 SCOJH CONSULT SRL — legal@scojhconsult.com
