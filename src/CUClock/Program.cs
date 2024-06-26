using System.Globalization;
using CUClock.Shared;

var esMX = new CultureInfo("es-MX");
CultureInfo.CurrentCulture = esMX;
CultureInfo.CurrentUICulture = esMX;
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Announcer>();
    })
    .Build();
host.Run();
