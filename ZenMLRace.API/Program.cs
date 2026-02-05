using Microsoft.EntityFrameworkCore;
using ZenMLRace.Core.Interfaces;
using ZenMLRace.Core.Services;
using ZenMLRace.Infrastructure.Data;
using ZenMLRace.Infrastructure.Scrapers;
using ZenMLRace.Infrastructure.Services;
using ZenMLRace.Worker;

var builder = WebApplication.CreateBuilder(args);

// DB Context の登録
builder.Services.AddDbContext<ZenMLDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Core/Infrastructure サービスの登録
builder.Services.AddSingleton<IJobQueue, ChannelJobQueue>();
builder.Services.AddScoped<IRaceCollector, PlaywrightRaceCollector>();

// Background Worker の登録 (メモリ内 Channel を共有するため API プロセス内で実行)
builder.Services.AddHostedService<Worker>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
