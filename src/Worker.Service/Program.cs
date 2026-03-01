using Application;
using Infrastructure;
using Serilog;
using Worker.Service.Outbox;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddDomainEventHandlers();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOutboxProcessing();

builder.Services.AddHostedService<OutboxBackgroundService>();

var host = builder.Build();

await host.RunAsync();
