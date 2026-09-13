// -----------------------------------------------------------------------
// <copyright company="SCOJH CONSULT">
//     Copyright (c) 2024-2026 SCOJH CONSULT SRL. All rights reserved.
//     PROPRIETARY AND CONFIDENTIAL. Unauthorized copying, distribution or
//     use of this file, via any medium, is strictly prohibited.
//     NO AI TRAINING: this code may NOT be used to train AI/ML models.
//     See LICENSE file in the project root for full licence information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Sdk.Attributes;
using Nexus.Sdk.Client;
using Nexus.Sdk.Configuration;
using Nexus.Sdk.Models;

namespace Nexus.Sdk.Services;

/// <summary>
/// IHostedService that runs on startup to:
/// 1. Scan assemblies for [NexusFeature], [NexusAuthorize], [NexusTrack] attributes
/// 2. Register discovered features and permissions with Nexus
/// 3. Pre-populate the feature config cache with the response
/// </summary>
public sealed class NexusRegistrationHostedService : IHostedService
{
    private readonly INexusClient _client;
    private readonly IFeatureService _featureService;
    private readonly NexusOptions _options;
    private readonly ILogger<NexusRegistrationHostedService> _logger;

    public NexusRegistrationHostedService(
        INexusClient client,
        IFeatureService featureService,
        IOptions<NexusOptions> options,
        ILogger<NexusRegistrationHostedService> logger)
    {
        _client = client;
        _featureService = featureService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableAutoRegistration)
        {
            _logger.LogInformation("Nexus auto-registration is disabled");
            return;
        }

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("Nexus API key not configured, skipping auto-registration");
            return;
        }

        try
        {
            var (features, permissions) = ScanAssemblies();

            if (features.Count == 0 && permissions.Count == 0)
            {
                _logger.LogInformation("No Nexus attributes found, skipping registration");
                return;
            }

            _logger.LogInformation(
                "Discovered {FeatureCount} features and {PermissionCount} permissions, registering with Nexus",
                features.Count, permissions.Count);

            var request = new SdkRegistrationRequest(
                _options.ApplicationId,
                _options.Environment,
                _options.Version,
                features,
                permissions);

            var response = await _client.RegisterAsync(request, cancellationToken);

            if (response?.Features is not null)
            {
                _featureService.SetBulk(response.Features);
                _logger.LogInformation("Successfully registered with Nexus, cached {Count} feature configs", response.Features.Count);
            }
            else
            {
                _logger.LogWarning("Registration returned no feature configs, using defaults");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to register with Nexus on startup, SDK will use defaults");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private (List<FeatureRegistration> Features, List<PermissionRegistration> Permissions) ScanAssemblies()
    {
        var features = new Dictionary<string, FeatureRegistration>();
        var permissions = new Dictionary<string, PermissionRegistration>();

        var assemblies = GetAssembliesToScan();

        foreach (var assembly in assemblies)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    ScanType(type, features, permissions);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types may not load — scan what we can
                foreach (var type in ex.Types.Where(t => t is not null))
                {
                    ScanType(type!, features, permissions);
                }
            }
        }

        return (features.Values.ToList(), permissions.Values.ToList());
    }

    private static void ScanType(
        Type type,
        Dictionary<string, FeatureRegistration> features,
        Dictionary<string, PermissionRegistration> permissions)
    {
        // Scan class-level attributes
        ScanAttributes(type, $"{type.Name}", features, permissions);

        // Scan method-level attributes
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            ScanAttributes(method, $"{type.Name}.{method.Name}", features, permissions);
        }
    }

    private static void ScanAttributes(
        MemberInfo member,
        string source,
        Dictionary<string, FeatureRegistration> features,
        Dictionary<string, PermissionRegistration> permissions)
    {
        foreach (var attr in member.GetCustomAttributes<NexusFeatureAttribute>())
        {
            features.TryAdd(attr.FeatureKey, new FeatureRegistration(attr.FeatureKey, source));
        }

        foreach (var attr in member.GetCustomAttributes<NexusAuthorizeAttribute>())
        {
            var code = attr.Permission ?? attr.Role;
            if (!string.IsNullOrEmpty(code))
            {
                permissions.TryAdd(code, new PermissionRegistration(code, source));
            }
        }

        // Also scan NexusTrack — register as a feature with tracking-only behavior
        foreach (var attr in member.GetCustomAttributes<NexusTrackAttribute>())
        {
            features.TryAdd(attr.EventName, new FeatureRegistration(attr.EventName, source));
        }
    }

    private IEnumerable<Assembly> GetAssembliesToScan()
    {
        if (_options.AssembliesToScan.Length > 0)
        {
            foreach (var name in _options.AssembliesToScan)
            {
                Assembly? assembly = null;
                try
                {
                    assembly = Assembly.Load(name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load assembly {Name} for scanning", name);
                }

                if (assembly is not null)
                    yield return assembly;
            }
        }
        else
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry is not null)
                yield return entry;
        }
    }
}
