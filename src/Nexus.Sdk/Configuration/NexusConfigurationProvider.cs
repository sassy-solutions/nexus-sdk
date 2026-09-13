// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Nexus.Sdk.Client;

namespace Nexus.Sdk.Configuration;

/// <summary>
/// The <see cref="IConfigurationProvider"/> that surfaces Nexus config keys through the standard .NET
/// <c>IConfiguration</c> system (so <c>builder.Configuration["ConnectionStrings:Database"]</c>, options
/// binding, <c>GetConnectionString</c> etc. all work). Nexus keys using <c>:</c> or <c>__</c> separators
/// map to the <c>IConfiguration</c> hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// Two phases. <b>Bootstrap</b> (<see cref="Load"/>): a one-shot fetch at build time, before DI exists,
/// so values are present for the earliest options binding. <b>Refresh</b>: once the host is running,
/// <see cref="Services.NexusConfigRefreshService"/> publishes each new snapshot through
/// <see cref="NexusConfigProviderBridge"/> to <see cref="ApplySnapshot"/>, which re-projects the data
/// and raises the reload token — no second HTTP client, no polling here.
/// </para>
/// <para>
/// <b>Precedence.</b> <c>AddNexus(IConfigurationBuilder ...)</c> inserts this source immediately BEFORE
/// the environment-variables source, so the effective order is: process environment (pod) &gt; Nexus &gt;
/// appsettings. A <c>MountedSecret</c> entry arrives with a <c>null</c> value in the manifest and is SKIPPED
/// here (never set to empty) — its real value reaches the app through the process environment.
/// </para>
/// </remarks>
public sealed class NexusConfigurationProvider : ConfigurationProvider
{
    private readonly NexusOptions _options;

    public NexusConfigurationProvider(NexusOptions options)
    {
        _options = options;
        NexusConfigProviderBridge.Register(this);
    }

    /// <summary>Bootstrap one-shot fetch (build time, pre-DI). Best-effort: unreachable Nexus → empty.</summary>
    public override void Load()
    {
        try
        {
            LoadAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort: if Nexus is unreachable during startup, continue with whatever the other
            // configuration sources provide. Required-key enforcement (fail-closed) is the refresh
            // service's job once the host is up — the provider never blocks the build.
        }
    }

    /// <summary>
    /// Re-projects a refreshed snapshot into <c>IConfiguration</c> and raises the reload token. Called by
    /// <see cref="NexusConfigProviderBridge"/> on the running host; skips <c>null</c>-valued entries
    /// (MountedSecret) so they are absent from config rather than blanked.
    /// </summary>
    internal void ApplySnapshot(ConfigSnapshot snapshot)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, entry) in snapshot.Entries)
        {
            if (entry.Value is null)
            {
                continue; // MountedSecret (or otherwise unresolved) — not delivered on this channel.
            }

            data[NormalizeKey(entry.Key)] = entry.Value;
        }

        Data = data;
        OnReload();
    }

    private async Task LoadAsync()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        };

        client.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(_options.ApplicationId))
            client.DefaultRequestHeaders.Add("X-Nexus-App-Id", _options.ApplicationId);
        if (!string.IsNullOrEmpty(_options.Environment))
            client.DefaultRequestHeaders.Add("X-Nexus-Env", _options.Environment);
        if (!string.IsNullOrEmpty(_options.Version))
            client.DefaultRequestHeaders.Add("X-Nexus-Version", _options.Version);

        var response = await client.GetAsync(NexusRoutes.SdkConfig);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var manifest = await response.Content.ReadFromJsonAsync<NexusConfigManifestDto>();
        if (manifest?.Entries is null)
        {
            return;
        }

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Entries)
        {
            if (entry.Value is null)
            {
                continue; // MountedSecret (null value in the manifest) — skip, do not blank.
            }

            data[NormalizeKey(entry.Key)] = entry.Value;
        }

        Data = data;
    }

    // Nexus keys use ":" or "__" as separators (e.g. "ConnectionStrings__Database"); the IConfiguration
    // hierarchy uses ":".
    private static string NormalizeKey(string key) => key.Replace("__", ConfigurationPath.KeyDelimiter);
}
