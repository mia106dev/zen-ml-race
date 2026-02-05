using ZenMLRace.Core.Interfaces;
using ZenMLRace.Core.Services;
using ZenMLRace.Infrastructure.Scrapers;
using ZenMLRace.Infrastructure.Services;
using ZenMLRace.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Service 登録
builder.Services.AddSingleton<IJobQueue, ChannelJobQueue>();
builder.Services.AddScoped<IRaceCollector, PlaywrightRaceCollector>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
