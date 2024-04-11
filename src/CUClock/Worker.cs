using Cronos;
using Humanizer;

namespace CUClock;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly Dictionary<CronExpression, Action> _schedules;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _schedules = new Dictionary<CronExpression, Action>();
        _schedules.Add(CronExpression.Hourly, EnPunto);
        _schedules.Add(
            CronExpression.Parse("60/4 * * * * *"),
            CuartoDeHora);
    }

    private void EnPunto()
    {
        _logger.LogInformation("La hora actual es: {hora}",
            DateTime.Now.TimeOfDay.Humanize());


        _logger.LogInformation("Son las {hora} {meridiano} en punto.",
            DateTime.Now.TimeOfDay.Hours < 13
                ? DateTime.Now.TimeOfDay.Hours
                : DateTime.Now.TimeOfDay.Hours - 12,
            DateTime.Now.TimeOfDay.Hours < 13
                ? "del día"
                : "de la noche");
    }

    private void CuartoDeHora()
    {
        _logger.LogInformation("Son las {hora} y cuarto.",
            DateTime.Now.TimeOfDay.Hours < 13
                ? DateTime.Now.TimeOfDay.Hours
                : DateTime.Now.TimeOfDay.Hours - 12);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
