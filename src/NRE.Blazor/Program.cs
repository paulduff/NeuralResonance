var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHttpClient("nre", (sp, c) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Api:BaseUrl"] ?? "http://localhost:5005/";
    c.BaseAddress = new Uri(baseUrl);
});

// Use the named client as the default HttpClient for components (enables relative URIs)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("nre"));
builder.Services.AddScoped<NRE.Blazor.Services.EngineApiClient>();
builder.Services.AddScoped<NRE.Blazor.Services.IEngineApiClient>(sp => sp.GetRequiredService<NRE.Blazor.Services.EngineApiClient>());
builder.Services.AddScoped<NRE.Blazor.Services.RendererInteropService>();
builder.Services.AddScoped<NRE.Blazor.Services.IRendererInteropService>(sp => sp.GetRequiredService<NRE.Blazor.Services.RendererInteropService>());
builder.Services.AddScoped<NRE.Blazor.Services.ConsoleRefreshCoordinator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
