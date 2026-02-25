using Npgsql;
using Worker.Service.Outbox;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' not found.");

    return new NpgsqlDataSourceBuilder(connectionString).Build();
});
// builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Connection string 'Database' not found."));


builder.Services.AddScoped<OutboxProcessor>();

builder.Services.AddHostedService<OutboxBackgroundService>();

var host = builder.Build();

await host.RunAsync();
