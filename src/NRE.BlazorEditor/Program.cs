using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using NRE.BlazorEditor.Components;
using NRE.BlazorEditor.Services;
using NRE.WorldSim;

var builder = WebApplication.CreateBuilder(args);
var editorOptions = EditorHostOptions.FromConfiguration(builder.Configuration);

builder.WebHost.ConfigureKestrel(options =>
{
    if (editorOptions.ListenAnyIp)
    {
        options.ListenAnyIP(editorOptions.Port);
    }
    else
    {
        options.ListenLocalhost(editorOptions.Port);
    }
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(editorOptions);
builder.Services.AddSingleton(new HeadlessWorldRuntime(new HeadlessWorldOptions(
    editorOptions.ControlProgramBaseUri)));
builder.Services.AddHostedService<WorldRuntimeHostedService>();
builder.Services.AddSingleton<WorldStateReader>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/editor/login";
        options.Cookie.Name = "NRE.Editor.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/editor/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }

            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<ControlTelemetryClient>(client =>
    {
        client.BaseAddress = editorOptions.ControlProgramBaseUri;
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(2),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (editorOptions.TrustForwardedHeaders)
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'self'; frame-ancestors 'none'; object-src 'none'; " +
        "script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "connect-src 'self' ws: wss:; font-src 'self'; form-action 'self'";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    await next();
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.Use(async (context, next) =>
{
    if (editorOptions.RequiresAuthentication &&
        context.Request.Path.StartsWithSegments("/editor") &&
        !context.Request.Path.StartsWithSegments("/editor/login") &&
        !context.Request.Path.StartsWithSegments("/editor/session") &&
        !context.Request.Path.StartsWithSegments("/editor/api") &&
        context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.Redirect("/editor/login");
        return;
    }

    await next();
});

app.MapGet("/", () => Results.Redirect("/editor"));

app.MapPost("/editor/session", async (
    HttpContext context,
    EditorHostOptions options,
    IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    if (!options.RequiresAuthentication)
    {
        return Results.Redirect("/editor");
    }

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var suppliedKey = form["accessKey"].ToString();
    if (!options.IsValidAccessKey(suppliedKey))
    {
        return Results.Redirect("/editor/login?error=1");
    }

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "DNNE editor operator")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
        });

    return Results.Redirect("/editor");
}).AllowAnonymous();

app.MapPost("/editor/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/editor/login");
});

var editorApi = app.MapGroup("/editor/api");
if (editorOptions.RequiresAuthentication)
{
    editorApi.RequireAuthorization();
}

editorApi.MapGet("/startup-health", (HttpContext context, ControlTelemetryClient control) =>
    control.CopyGetAsync(context, "/api/v1/startup-health?maxNonOkDetails=12"));
editorApi.MapGet("/service-health", (HttpContext context, ControlTelemetryClient control) =>
    control.CopyGetAsync(context, "/api/v1/service-health"));
editorApi.MapGet("/frame", (HttpContext context, ControlTelemetryClient control) =>
    control.CopyGetAsync(
        context,
        "/api/v1/frame?include_connectome=0&max_output_log=80&max_spike_log=120&max_dispatch_spikes=180"));
editorApi.MapGet("/world-state", (WorldStateReader reader, CancellationToken cancellationToken) =>
    reader.ReadAsync(cancellationToken));
editorApi.MapPost("/world/resume", (HeadlessWorldRuntime runtime) =>
{
    runtime.Resume();
    return Results.Ok(new { accepted = true, running = true });
});
editorApi.MapPost("/world/pause", (HeadlessWorldRuntime runtime) =>
{
    runtime.Pause();
    return Results.Ok(new { accepted = true, running = false });
});
editorApi.MapPost("/world/reset", (HeadlessWorldRuntime runtime) =>
{
    runtime.Reset();
    return Results.Ok(new { accepted = true, running = true });
});
editorApi.MapPost("/admin/shutdown", async (
    HeadlessWorldRuntime runtime,
    IHostApplicationLifetime lifetime) =>
{
    runtime.Pause();
    await runtime.StopAsync();
    _ = Task.Run(async () =>
    {
        await Task.Delay(100).ConfigureAwait(false);
        lifetime.StopApplication();
    });
    return Results.Ok(new
    {
        accepted = true,
        reportPath = runtime.LastRunReportPath,
        reportError = runtime.LastRunReportError
    });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
