using ActivityTracker.Configuration;
using ActivityTracker.Data;
using ActivityTracker.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace ActivityTracker.Services;

public sealed class SyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncService> _logger;
    private readonly HttpClient _client;
    private readonly AppSettings _settings;

    public SyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> settings,
        ILogger<SyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;

        _client = httpClientFactory.CreateClient("ActivityApi");
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Sync Service Started. Interval: {SyncMinutes} minute(s)",
            _settings.Api.SyncMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncActivitiesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Catch-all so a single bad sync cycle can never silently kill the loop.
                _logger.LogError(ex, "Sync failed");
            }

            try
            {
                // Heartbeat: confirms the service is alive and waiting,
                // instead of leaving a silent gap that looks identical to a hang/crash.
                _logger.LogInformation(
                    "Sync Service heartbeat. Next sync in {SyncMinutes} minute(s)",
                    _settings.Api.SyncMinutes);

                await Task.Delay(
                    TimeSpan.FromMinutes(_settings.Api.SyncMinutes),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Sync Service Stopped");
    }

    private async Task SyncActivitiesAsync(
        CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository =
            scope.ServiceProvider
                 .GetRequiredService<IRepository>();

        var activities =
            await repository.GetUnsyncedAsync(
                _settings.Api.BatchSize,
                token);

        if (activities.Count == 0)
        {
            _logger.LogDebug("No activities to sync");
            return;
        }

        HttpResponseMessage response;

        try
        {
            // Wrap activities in the expected request schema
            var payload = activities.ToSyncRequest();

            // If the named client was configured with a BaseAddress, post to the base.
            if (!string.IsNullOrWhiteSpace(_client.BaseAddress?.ToString()))
            {
                response = await _client.PostAsJsonAsync(string.Empty, payload, token);
            }
            else
            {
                response = await _client.PostAsJsonAsync(_settings.Api.BaseUrl, payload, token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post activities to API");
            return;
        }

        if (response.IsSuccessStatusCode)
        {
            await repository.MarkSyncedAsync(
                activities.Select(x => x.Id).ToList(),
                token);

            _logger.LogInformation(
                "Successfully uploaded {Count} activities",
                activities.Count);

            return;
        }

        var error = await response.Content.ReadAsStringAsync(token);

        _logger.LogWarning(
            "Sync failed. Status: {Status}. Response: {Response}",
            response.StatusCode,
            error);
    }
}