using WakeOnLanProxy;


// convert to service 
// https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => { services.AddHostedService<Worker>(); })
    .Build();

host.Run();