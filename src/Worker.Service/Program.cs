using Application;
using Infrastructure;
using Worker.Service.Outbox;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDomainEventHandlers();
builder.Services.AddOutboxProcessing(builder.Configuration);

builder.Services.AddHostedService<OutboxBackgroundService>();

var host = builder.Build();

await host.RunAsync();
