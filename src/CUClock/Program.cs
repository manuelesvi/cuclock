using System.Globalization;
using CUClock;

var esMX = new CultureInfo("es-MX");
CultureInfo.CurrentCulture = esMX;
CultureInfo.CurrentUICulture = esMX;
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();
host.Run();
