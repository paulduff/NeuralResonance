using NRE.Core.Engine;
using NRE.Api.Serialization;
using NRE.Api.Services;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // PERF: Use source-generated metadata for hot DTOs (framefast, etc.)
    o.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, NreJsonContext.Default);
    o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
});

builder.Services.AddSingleton(new NreEngine(new NreEngineOptions(), seed: 12345));

// PERF: cached serialized JSON for fast-frame polling.
builder.Services.AddSingleton<FastFrameJsonCache>();
builder.Services.AddSingleton<FastFrameBinaryCache>();

// Simple simulation loop hosted service
builder.Services.AddHostedService<EngineLoopHostedService>();

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseCors();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public sealed class EngineLoopHostedService : BackgroundService
{
    private readonly NreEngine _engine;
    private Thread? _thread;

    public EngineLoopHostedService(NreEngine engine) => _engine = engine;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run simulation on a dedicated thread, not the ASP.NET thread pool.
        // This prevents thread pool starvation between sim and HTTP requests.
        _thread = new Thread(() => SimLoop(stoppingToken))
        {
            Name = "NRE-SimLoop",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        var thread = _thread;
        if (thread is not null && thread.IsAlive)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    private void SimLoop(CancellationToken ct)
    {
        const float dt = 1f / 60f;
        const double targetFrameMs = 1000.0 / 60.0;
        var sw = new System.Diagnostics.Stopwatch();

        while (!ct.IsCancellationRequested)
        {
            sw.Restart();
            _engine.Step(dt);
            sw.Stop();

            var sleepMs = (int)Math.Round(targetFrameMs - sw.Elapsed.TotalMilliseconds);
            if (sleepMs > 0 && ct.WaitHandle.WaitOne(sleepMs))
            {
                break;
            }
        }
    }
}