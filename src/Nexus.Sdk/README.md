# Nexus.Sdk

The .NET client for the Nexus platform: remote configuration, feature gating, usage and
billing events, permission checks, and — opt-in — the admin/tooling management surface.

```csharp
builder.Services.AddNexus(builder.Configuration);
builder.Services.AddControllers(options => options.AddNexusFilters());
```

```jsonc
{
  "Nexus": {
    "BaseUrl": "https://api.nexus.example.com",  // required, no default
    "ApiKey": "nxs_…"
  }
}
```

---

## One entry point

`AddNexus` is the only registration method. `grep -rn "public static IServiceCollection Add"
src/SDK` returns exactly one line, and a test keeps it that way.

It registers the lean app-runtime surface: the config snapshot and its refresh loop, feature
evaluation, usage and billing, auto-registration, the health check (tagged `nexus`, **not**
`ready` — a platform blip must not drain traffic from healthy tenant pods), and the three MVC
filters.

## Feature flags through the Compendium port

`AddNexus` also registers Nexus as an `IFeatureFlags` provider
(`Compendium.Abstractions.FeatureFlags`), so flags can be read through the framework contract
instead of through Nexus-shaped types:

```csharp
public sealed class ExportService(IFeatureFlags flags)
{
    public async Task<bool> CanExport(string tenantId, string userId, CancellationToken ct)
    {
        // The port takes the token positionally — it declares no default for it.
        var result = await flags.IsOnAsync("reports.export", new FlagContext(tenantId, userId), ct);

        // Closed when Nexus had nothing to say — pick the fallback your feature deserves.
        return result.Match(enabled => enabled, _ => false);
    }
}
```

That is the point of it: nothing above names Nexus. Moving to LaunchDarkly, ConfigCat, or a
fake in a test is a registration change, not an edit of every call site. The registration uses
`TryAdd`, so an `IFeatureFlags` you register **before** `AddNexus` keeps winning.

**Two limits, because Nexus does not answer these questions.** The Nexus flag engine evaluates
booleans and carries neither typed variants nor an experiment registry:

| Member | Against Nexus |
|---|---|
| `IsOnAsync` | Fully served. |
| `GetVariantAsync<T>` | `T = bool` and `T = bool?` are evaluated. **Any other `T` returns your `defaultValue`**, without a network call. |
| `GetExperimentAsync` | **Always fails** (`FlagNotFound`), without a network call — Nexus runs no experiments. |

`GetExperimentAsync` fails rather than handing back an assignment with `InExperiment: false`,
which would be a positive answer ("evaluated, then left out of the cohort") to a question Nexus
never asked.

### What the port sends, and what it does not

`FlagContext` carries three things — tenant, user, attributes. Nexus evaluates seven, in this
order (`Nexus.Core.Domain.Policies.FeatureFlagPolicy`):

| Nexus signal | Where the port gets it |
|---|---|
| matrix gate (environment × version) | `Nexus:Environment` + `Nexus:Version` |
| version gate | `Nexus:Version` |
| environment gate | `Nexus:Environment` |
| user override | `FlagContext.UserId` |
| attribute override | `FlagContext.Attributes` |
| tier override | **nothing — see below** |
| flag default | the flag itself |

**Environment and version come from your configuration, not from the caller.** They are the
highest-priority signals Nexus evaluates and `FlagContext` has no field for either, so the port
reads the same `Nexus:Environment` / `Nexus:Version` that `[NexusFeature]` gates read. That is
what makes the two agree: without it, a flag switched Off for production would read On through
`IFeatureFlags` while the gate on the same flag, in the same process, held it shut.

**Tier is not sent, and the server's default is not neutral.** The check endpoint binds an absent
`tier` to `SubscriptionTier.Free` — it does not derive the tier from your API key — so a flag
carrying *tier overrides* is evaluated against `Free` through this port. Target per tier through
`FlagContext.Attributes` instead (`["tier"] = "pro"`, matched against the flag's **attribute**
overrides, which Nexus ranks above the tier branch anyway).

**`FlagContext.TenantId` is not sent.** Compendium makes it mandatory and caller-supplied; Nexus
derives the organization from the authenticated caller and accepts no tenant claim over the wire
(the `X-Organization-Id` header was removed for exactly that reason). Pass it — the port requires
it — but the scope actually evaluated is the scope of your `Nexus:ApiKey`.

**When Nexus cannot be reached**, `IsOnAsync` falls back to the flag's *global* default
enablement — every gate and override ignored, because none can be evaluated without the server —
and fails with `FlagNotFound` if even that is unavailable. It never reports `ProviderUnreachable`:
`INexusClient` answers `null` for both "no such flag" and "unreachable", so the two are
indistinguishable at this layer.

## `Nexus:BaseUrl` is required

There is no default, deliberately. The SDK used to fall back to an in-platform DNS name of the
production API namespace. Outside that cluster, forgetting the key did not produce a
configuration error — it produced a DNS failure at the first request, reported as a 503 carrying
an exception message, with nothing pointing at the configuration.

Now a missing key fails the host at startup with an `OptionsValidationException` naming
`Nexus:BaseUrl` (environment variable `Nexus__BaseUrl`). The pre-host configuration source
(`builder.Configuration.AddNexus(...)`) cannot use `ValidateOnStart`, so it throws the same
message itself.

## Permission checks have three outcomes

`INexusClient.CheckPermissionAsync` returns a `NexusPermissionDecision`, not a `bool`:

| Decision | Meaning |
|---|---|
| `Allowed` | Nexus answered yes. |
| `Denied` | Nexus answered no — including 401 on an expired key and 403. |
| `Indeterminate` | Nexus did not answer: unreachable, timed out, 5xx, or an unreadable body. |

It used to return `bool`, and `false` covered all four of "no", "expired key", "server error"
and "no response at all". `[NexusAuthorize]` turned that single `false` into
`403 You do not have the required permission: X`. So a network cut between a tenant application
and Nexus made every annotated endpoint of that application state something untrue, and the
operator went looking at roles.

### What `[NexusAuthorize]` does with each

| Decision | Response |
|---|---|
| `Allowed` | The request proceeds. |
| `Denied` | **403**, `Title: "Forbidden"`, `Detail: "You do not have the required permission: X"` — unchanged. |
| `Indeterminate` (default) | **503** with `Retry-After`, `Title: "Nexus unreachable"`, and `Extensions["code"] == "nexus.permission.indeterminate"`. The detail names the platform and never the permission. |
| `Indeterminate`, with `Allow` configured | The request proceeds, logged at `Warning`. |

**The default is closed and it is a deliberate choice.** A Nexus outage makes annotated
endpoints unavailable rather than wrong; the alternative — letting the request through — would
turn a network cut into a privilege escalation across every deployed tenant application at once.
The cost is real: those endpoints are down while Nexus is down.

To choose the opposite for a surface where availability outranks the permission:

```csharp
builder.Services.AddNexus(builder.Configuration,
    o => o.OnPermissionUnavailable = NexusPermissionUnavailableBehavior.Allow);
```

or `Nexus:OnPermissionUnavailable = "Allow"` in configuration. Understand what it means before
setting it: while Nexus is unreachable, every caller holds every annotated permission on this
application.

This mirrors `Nexus:DefaultFeatureEnabled`, which is the same question asked about feature
flags.

## Governance is a separate package

This package is what a tenant application takes to be **governed by** Nexus: configuration,
feature flags, usage and billing, permission checks, health, startup registration. It carries
**no administration surface at all**, and that is enforced, not merely intended — a contract
test fails the build if its route registry ever names a route under `admin/` or `platform/`.

Administration — organizations, projects, applications, environments, roles, git connections,
platform resources — lives in **`SassySolutions.Nexus.Management`**, a separate package with its
own `AddNexusManagement(configuration)` entry point.

The split is not packaging taste. Those routes are gated by administrator policies and refuse an
API-key principal outright: an API key carries scopes, not roles. A tenant application that has
no administrator token could only ever be refused by them, so it should not be able to name them.

## The SDK never throws for an expected failure

Every management method returns `NexusResult<T>`; a 404, a 403 and a network error are all
failed results carrying a status code, never exceptions. `NexusRoutes.Of` is the one exception
to the exception: it throws on a placeholder/argument mismatch, which is a programming error and
cannot depend on anything the platform does.

## Licence

This SDK is source-available, not open source. Reading it is free; using it requires a Nexus
licence. See [LICENSE](LICENSE).

Copyright (c) 2024-2026 SCOJH CONSULT SRL — legal@scojhconsult.com
