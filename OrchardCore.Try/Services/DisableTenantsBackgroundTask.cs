using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrchardCore.Try.Services;

/// <summary>
/// Background service that disables all active tenants except Default on Sunday at 11:59pm UTC
/// </summary>
public class DisableTenantsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DisableTenantsBackgroundService> _logger;

    public DisableTenantsBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DisableTenantsBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DisableTenantsBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextSunday = GetNextSundayAt2359(now);
                var delay = nextSunday - now;

                _logger.LogInformation("Next tenant disable scheduled for {NextRun} UTC (in {Hours} hours, {Minutes} minutes)", 
                    nextSunday, delay.TotalHours, delay.TotalMinutes);

                // Wait until the next scheduled time
                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await DisableTenantsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                _logger.LogInformation("DisableTenantsBackgroundService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DisableTenantsBackgroundService");
                // Wait 5 minutes before retrying to avoid rapid failure loops
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private static DateTime GetNextSundayAt2359(DateTime from)
    {
        // Find next Sunday at 23:59:00 UTC
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)from.DayOfWeek + 7) % 7;

        // If it's Sunday but past 23:59, go to next Sunday
        if (daysUntilSunday == 0)
        {
            var todayAt2359 = new DateTime(from.Year, from.Month, from.Day, 23, 59, 0, DateTimeKind.Utc);
            if (from >= todayAt2359)
            {
                daysUntilSunday = 7;
            }
        }

        var nextSunday = from.Date.AddDays(daysUntilSunday);
        return new DateTime(nextSunday.Year, nextSunday.Month, nextSunday.Day, 23, 59, 0, DateTimeKind.Utc);
    }

    private async Task DisableTenantsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var shellHost = scope.ServiceProvider.GetRequiredService<IShellHost>();
        var shellSettingsManager = scope.ServiceProvider.GetRequiredService<IShellSettingsManager>();

        _logger.LogInformation("Starting weekly tenant disable task at {Time} UTC", DateTime.UtcNow);

        var allShellSettings = shellHost.GetAllSettings();
        var tenantsToDisable = allShellSettings
            .Where(s => !string.Equals(s.Name, "Default", StringComparison.OrdinalIgnoreCase) 
                     && s.State == TenantState.Running)
            .ToList();

        _logger.LogInformation("Found {Count} tenants to disable (excluding Default)", tenantsToDisable.Count);

        foreach (var shellSettings in tenantsToDisable)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                _logger.LogInformation("Disabling tenant: {TenantName}", shellSettings.Name);

                // Update the shell settings to set the state to Disabled
                shellSettings.State = TenantState.Disabled;
                await shellSettingsManager.SaveSettingsAsync(shellSettings);

                // Release the shell context to stop the tenant
                await shellHost.ReleaseShellContextAsync(shellSettings);

                _logger.LogInformation("Successfully disabled tenant: {TenantName}", shellSettings.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling tenant: {TenantName}", shellSettings.Name);
            }
        }

        _logger.LogInformation("Completed weekly tenant disable task. Disabled {Count} tenants at {Time} UTC", 
            tenantsToDisable.Count, DateTime.UtcNow);
    }
}
