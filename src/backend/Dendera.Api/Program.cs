using System.Text.Json.Serialization;
using Dendera.Api.Calibration;
using Dendera.Api.Data;
using Dendera.Api.Endpoints;
using Dendera.Api.Services;
using Dendera.Core.Astro;
using Dendera.Core.Signals;
using Dendera.Sources;
using Microsoft.EntityFrameworkCore;

// Release ritual (docs/plans/calibration.md), not part of the web app:
//   fetch-reference  download the Kp and weather history into src/backend/calibration (network, gitignored) and check
//                    it against calibration/manifest.json; --update-manifest accepts new data from the sources
//   calibrate        write rules/<school>.quantiles.json and docs/calibration-report.md (offline, deterministic)
if (args.Length > 0 && args[0] is "calibrate" or "fetch-reference")
{
    var root = CalibrationRitual.FindBackendRoot(Directory.GetCurrentDirectory());
    var ritual = new CalibrationRitual(root, Console.Out);
    try
    {
        if (args[0] == "fetch-reference")
            return await ReferenceFixtures.FetchAsync(ritual.FixturesDir, Dendera.Core.Calibration.ReferenceSet.Default,
                args.Contains("--update-manifest"), Console.Out) ? 0 : 1;
        ritual.Run();
        return 0;
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
});

services.AddDbContext<DenderaDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Dendera") ?? "Data Source=dendera.db"));

services.AddMemoryCache();
services.AddSingleton(TimeProvider.System);

// Core (pure)
services.AddSingleton<IEphemeris, AstronomyEngineEphemeris>();
services.AddSingleton(sp => new RuleSetCatalog(
    Path.Combine(AppContext.BaseDirectory, builder.Configuration["Dendera:RulesDirectory"] ?? "rules"),
    sp.GetServices<ISignalProvider>().Select(p => p.Info),
    sp.GetRequiredService<ILoggerFactory>().CreateLogger<RuleSetCatalog>()));
services.AddSingleton<InfluenceStatusBoard>();
services.AddSingleton<ProfileContextFactory>();
services.AddScoped<SignalHub>();

// Influences: register every ISignalProvider here (pure ones from the Core, impure ones from Dendera.Sources).
services.AddSingleton<ISignalProvider, EphemerisProvider>();
services.AddSingleton<ISignalProvider, TcmCalendarProvider>();
services.AddSingleton<ISignalProvider, HumanDesignProvider>();
const string userAgent = "Dendera/0.1 (self-hosted)";
services.AddHttpClient<GfzKpProvider>(c =>
{
    c.BaseAddress = new Uri(GfzKpProvider.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
});
services.AddHttpClient(OpenMeteoWeatherProvider.ForecastClient, c =>
{
    c.BaseAddress = new Uri("https://api.open-meteo.com/");
    c.Timeout = TimeSpan.FromSeconds(20);
});
services.AddHttpClient(OpenMeteoWeatherProvider.ArchiveClient, c =>
{
    c.BaseAddress = new Uri("https://archive-api.open-meteo.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
});
services.AddHttpClient<OpenMeteoGeocoder>(c =>
{
    c.BaseAddress = new Uri(OpenMeteoGeocoder.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(15);
});
services.AddHttpClient<SchumannProvider>(c =>
{
    c.BaseAddress = new Uri(SchumannProvider.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
});
services.AddTransient<ISignalProvider>(sp => sp.GetRequiredService<GfzKpProvider>());
services.AddTransient<ISignalProvider, OpenMeteoWeatherProvider>();
services.AddTransient<ISignalProvider>(sp => sp.GetRequiredService<SchumannProvider>());

services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DenderaDb>().Database.Migrate();
    _ = scope.ServiceProvider.GetRequiredService<RuleSetCatalog>(); // fail fast on broken rule files
}

app.UseCors();
app.MapDenderaApi();

// Serve the built Angular app (src/frontend → wwwroot) when present, for single-process self-hosting.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
return 0;
