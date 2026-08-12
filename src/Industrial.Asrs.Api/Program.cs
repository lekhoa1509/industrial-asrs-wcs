using Industrial.Asrs.Domain;
using Industrial.Asrs.Infrastructure;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<GridPathPlanner>();
builder.Services.AddSingleton<IShuttleDevice>(_ => new SimulatedShuttle("SH-01", new(Zone.A, 1, 0), seed: 11));
builder.Services.AddSingleton<IShuttleDevice>(_ => new SimulatedShuttle("SH-02", new(Zone.B, 12, 0), seed: 22));
builder.Services.AddSingleton<WarehouseControlSystem>();

WebApplication app = builder.Build(); app.UseCors();
WarehouseControlSystem wcs = app.Services.GetRequiredService<WarehouseControlSystem>(); await wcs.StartAsync();
app.MapGet("/health", () => Results.Ok(new { status = "ok", mode = "simulator" }));
app.MapGet("/api/state", (WarehouseControlSystem system, CancellationToken token) => system.SnapshotAsync(token));
app.MapPost("/api/orders", (TransportOrder order, WarehouseControlSystem system) => { system.Enqueue(order); _ = Task.Run(() => system.ProcessNextAsync(CancellationToken.None), CancellationToken.None); return Results.Accepted($"/api/orders/{order.OrderId}"); });
app.MapPost("/api/reset", async (WarehouseControlSystem system, CancellationToken token) => { await system.ResetAsync(token); return Results.NoContent(); });
app.Run();
