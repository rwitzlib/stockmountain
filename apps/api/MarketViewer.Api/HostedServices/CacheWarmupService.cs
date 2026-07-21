using MarketViewer.Api.Jobs;
using Quartz;

namespace MarketViewer.Api.HostedServices
{
    /// <summary>
    /// Schedules all recurring jobs on fixed cron schedules in the exchange timezone,
    /// so nothing depends on what time the process happened to start. The warmup
    /// itself runs inside WarmupJob (immediate + daily + recovery triggers); while it
    /// runs, SnapshotJob buffers snapshots via CacheWarmupState.
    /// </summary>
    public class CacheWarmupService(
        ISchedulerFactory schedulerFactory,
        ILogger<CacheWarmupService> logger) : IHostedService
    {
        private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        private static bool WarmupDisabled =>
            string.Equals(Environment.GetEnvironmentVariable("CACHE_WARMUP_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (WarmupDisabled)
            {
                logger.LogWarning("Cache warmup is disabled (CACHE_WARMUP_ENABLED=false); no jobs scheduled and /scan will have no market data.");
                return;
            }

            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);

            var warmupJob = JobBuilder.Create<WarmupJob>()
                .WithIdentity("warmup")
                .StoreDurably()
                .Build();
            await scheduler.AddJob(warmupJob, replace: true, cancellationToken);

            // Initial warmup as soon as the scheduler starts.
            await scheduler.ScheduleJob(TriggerBuilder.Create()
                .ForJob(warmupJob)
                .WithIdentity("warmup-startup")
                .StartNow()
                .UsingJobData(WarmupJob.ForceKey, true)
                .Build(), cancellationToken);

            // Daily rebuild at 3:30am ET: after the market-data aggregator has
            // published the previous session's files (~1-2am ET), before 4am
            // pre-market. Replaces cached responses wholesale, which bounds bar-list
            // growth and staleness. Runs weekends too so entries stay fresh.
            await scheduler.ScheduleJob(TriggerBuilder.Create()
                .ForJob(warmupJob)
                .WithIdentity("warmup-daily")
                .WithCronSchedule("0 30 3 ? * *", x => x.InTimeZone(TimeZone))
                .UsingJobData(WarmupJob.ForceKey, true)
                .Build(), cancellationToken);

            // Recovery: no-ops while the cache is ready, retries a failed warmup.
            await scheduler.ScheduleJob(TriggerBuilder.Create()
                .ForJob(warmupJob)
                .WithIdentity("warmup-recovery")
                .WithCronSchedule("0 0/5 * ? * *", x => x.InTimeZone(TimeZone))
                .UsingJobData(WarmupJob.ForceKey, false)
                .Build(), cancellationToken);

            // Snapshots every minute (3s in, so the provider has the bar) during
            // extended market hours. Buffered by CacheWarmupState while warming.
            await scheduler.ScheduleJob(
                JobBuilder.Create<SnapshotJob>().WithIdentity("snapshot").Build(),
                TriggerBuilder.Create()
                    .WithIdentity("snapshot-minutely")
                    .WithCronSchedule("3 * 4-19 ? * MON-FRI", x => x.InTimeZone(TimeZone))
                    .Build(), cancellationToken);

            // Scanner only runs while new bars are flowing; overnight the data is
            // frozen and still-true filters would re-fire the same signals all night.
            await scheduler.ScheduleJob(
                JobBuilder.Create<ScannerJob>().WithIdentity("scanner").Build(),
                TriggerBuilder.Create()
                    .WithIdentity("scanner-15s")
                    .WithCronSchedule("0/15 * 4-19 ? * MON-FRI", x => x.InTimeZone(TimeZone))
                    .Build(), cancellationToken);

            await scheduler.ScheduleJob(
                JobBuilder.Create<PopulateScannerJob>().WithIdentity("populate-scanner").Build(),
                TriggerBuilder.Create()
                    .WithIdentity("populate-scanner-5m")
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
                    .Build(), cancellationToken);

            logger.LogInformation("Scheduled warmup, snapshot, and scanner jobs (timezone: {timeZone}).", TimeZone.Id);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
