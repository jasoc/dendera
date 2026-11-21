using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dendera.Core;
using Dendera.Core.Astro;
using Dendera.Core.Calibration;
using Dendera.Core.Rules;
using Dendera.Core.Signals;
using Dendera.Sources;
using Hour = Dendera.Sources.OpenMeteoWeatherProvider.Hour;

namespace Dendera.Api.Calibration;

/// <summary>
/// The release ritual: for every school and body node, the distribution of raw sums over the synthetic reference
/// sky, written as quantile tables (rules/&lt;school&gt;.quantiles.json) plus docs/calibration-report.md.
/// Offline (fixtures only) and deterministic: same inputs, byte-identical files.
/// </summary>
public sealed class CalibrationRitual(string backendRoot, TextWriter log)
{
    public string RulesDir => Path.Combine(backendRoot, "rules");
    public string FixturesDir => Path.Combine(backendRoot, "calibration");
    public string ReportPath => Path.GetFullPath(Path.Combine(backendRoot, "..", "..", "docs", "calibration-report.md"));

    private static readonly IReadOnlyList<InfluenceInfo> Influences =
    [
        EphemerisProvider.Influence, TcmCalendarProvider.Influence, HumanDesignProvider.Influence,
        GfzKpProvider.Influence, OpenMeteoWeatherProvider.Influence, SchumannProvider.Influence,
    ];

    /// <summary>Walks up from <paramref name="start"/> to the directory holding Dendera.slnx (src/backend).</summary>
    public static string FindBackendRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dendera.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Run from inside src/backend (Dendera.slnx not found)");
    }

    /// <summary>Rule files of the schools (quantile tables excluded).</summary>
    public static IEnumerable<string> RuleFiles(string rulesDir) =>
        Directory.GetFiles(rulesDir, "*.json")
            .Where(f => !f.EndsWith(".quantiles.json", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);

    public static string QuantilesPath(string rulesDir, string schoolId) => Path.Combine(rulesDir, $"{schoolId}.quantiles.json");

    public void Run()
    {
        var total = Stopwatch.StartNew();
        var set = ReferenceSet.Default;
        var schools = RuleFiles(RulesDir).Select(f => RuleSetJson.Parse(File.ReadAllText(f))).ToList();
        foreach (var s in schools) RuleSetJson.ValidateInfluences(s, Influences);
        var referenceHash = CalibrationHash.Reference(set, ReferenceFixtures.Verify(FixturesDir));
        log.WriteLine($"Reference {referenceHash}: {schools.Count} schools, {set.Charts} charts, {set.Instants} instants, " +
                      $"{set.ClimateSamples} samples × {ClimateZones.All.Count} climate zones");

        var eph = new AstronomyEngineEphemeris();
        var births = set.Births();
        var charts = births.Select(b => ChartCalculator.Compute(eph, b)).ToList();
        var hdCharts = births.Select(b => HumanDesign.Compute(eph, b.BirthUtc)).ToList();
        var baselines = charts.Select((c, i) => NatalBaselines.For(schools, c, () => hdCharts[i].DefinedCenters)).ToList();
        var eclipses = eph.Eclipses(
            new DateTimeOffset(Math.Min(set.FromYear, set.DataFromYear) - 1, 12, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(Math.Max(set.ToYear, set.DataToYear) + 1, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var sources = new SignalSources(eph, eclipses);
        log.WriteLine($"Charts and {eclipses.Count} eclipses ready ({total.Elapsed.TotalSeconds:0}s)");

        var results = schools.ToDictionary(s => s.Id, _ => new SortedDictionary<string, TableResult>(StringComparer.Ordinal));

        // Schools that read only the sky: one global table over the long random span.
        var global = schools.Where(s => !ClimateZones.IsClimateDependent(s)).ToList();
        if (global.Count > 0)
        {
            var instants = set.SkyInstants();
            var raws = Sample(global, instants.Count, i =>
            {
                var c = i % set.Charts;
                var b = births[c];
                return (sources.Gather(Needs(global), instants[i], b.Latitude, b.Longitude, charts[c], hdCharts[c], null, null), baselines[c]);
            });
            foreach (var (s, r) in global.Zip(raws)) results[s.Id][SchoolCalibration.Global] = r;
            log.WriteLine($"Global: {string.Join(", ", global.Select(s => s.Id))} ({total.Elapsed.TotalSeconds:0}s)");
        }

        // Schools that read the local weather: one table per climate zone, sky and weather from the same real moment.
        var climate = schools.Where(ClimateZones.IsClimateDependent).ToList();
        if (climate.Count > 0)
        {
            var kp = ReferenceFixtures.LoadKp(FixturesDir, set);
            for (var z = 0; z < ClimateZones.All.Count; z++)
            {
                var zone = ClimateZones.All[z];
                var weather = ReferenceFixtures.LoadWeather(FixturesDir, zone, set);
                var instants = set.ClimateInstants(z);
                var raws = Sample(climate, instants.Count, i =>
                {
                    var c = i % set.Charts;
                    return (sources.Gather(Needs(climate), instants[i], zone.Latitude, zone.Longitude, charts[c], hdCharts[c], kp, weather),
                        baselines[c]);
                });
                foreach (var (s, r) in climate.Zip(raws)) results[s.Id][zone.Id] = r;
                log.WriteLine($"Climate {zone.Id}: {string.Join(", ", climate.Select(s => s.Id))} ({total.Elapsed.TotalSeconds:0}s)");
            }
        }

        foreach (var s in schools)
        {
            var tables = results[s.Id];
            var calibration = new SchoolCalibration
            {
                School = s.Id,
                SchoolVersion = s.Version,
                RulesHash = CalibrationHash.Rules(s),
                ReferenceHash = referenceHash,
                SignalsVersion = ReferenceSet.SignalsVersion,
                Samples = tables.Values.First().Samples,
                Tables = new SortedDictionary<string, SortedDictionary<string, double[]>>(
                    tables.ToDictionary(kv => kv.Key, kv => kv.Value.Quantiles), StringComparer.Ordinal),
            };
            File.WriteAllText(QuantilesPath(RulesDir, s.Id), calibration.ToJson(), new UTF8Encoding(false));
        }

        File.WriteAllText(ReportPath, Report(set, referenceHash, schools, results), new UTF8Encoding(false));
        log.WriteLine($"Wrote {schools.Count} quantile tables and {ReportPath} in {total.Elapsed.TotalSeconds:0}s");
    }

    private static HashSet<string> Needs(IEnumerable<RuleSet> schools) => schools.SelectMany(s => s.Requires).ToHashSet();

    /// <summary>Raw sum per school, node and sample (parallel, written by index: deterministic).</summary>
    private static List<TableResult> Sample(
        IReadOnlyList<RuleSet> schools, int count,
        Func<int, (IReadOnlyList<Signal> Signals, NatalBaselines Baselines)> at)
    {
        var nodes = schools.Select(s => BodyZones.Ordered.Where(s.Covers.Contains).ToList()).ToList();
        var raws = schools.Select((_, k) => nodes[k].Select(_ => new float[count]).ToArray()).ToArray();
        Parallel.For(0, count, i =>
        {
            var (signals, baselines) = at(i);
            for (var k = 0; k < schools.Count; k++)
            {
                var acts = RuleEngine.Evaluate(schools[k], signals, baselines.GetValueOrDefault(schools[k].Id));
                var byZone = acts.ToDictionary(a => a.Zone, a => a.Raw);
                for (var n = 0; n < nodes[k].Count; n++) raws[k][n][i] = (float)byZone[nodes[k][n]];
            }
        });
        return schools.Select((_, k) => TableResult.From(nodes[k], raws[k])).ToList();
    }

    // ---------- report ----------

    private static readonly (string Name, double Years)[] Cycles =
    [
        ("Luna (mese sinodico)", 29.53 / 365.25), ("Sole (anno)", 1), ("Nodi lunari ed eclissi (Saros)", 18.6),
        ("Giove", 11.86), ("Saturno", 29.46), ("Urano", 84.0), ("Nettuno", 164.8), ("Plutone", 248.0),
    ];

    private static string Report(
        ReferenceSet set, string referenceHash, IReadOnlyList<RuleSet> schools,
        Dictionary<string, SortedDictionary<string, TableResult>> results)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("# Calibration report");
        sb.AppendLine();
        sb.AppendLine("Generated by the ritual (`dotnet run --project Dendera.Api -- calibrate`, from `src/backend`). Do not edit by hand.");
        sb.AppendLine("Method and decisions: `docs/plans/calibration.md`.");
        sb.AppendLine();
        sb.AppendLine("## Reference set");
        sb.AppendLine();
        sb.AppendLine($"- Reference hash `{referenceHash}`, signals version {ReferenceSet.SignalsVersion}, seed {set.Seed}.");
        sb.AppendLine($"- {set.Charts} synthetic births {set.BirthFromYear}–{set.BirthToYear}, all hours, latitudes 0–{set.MaxLatitude:0}° N/S " +
                      "(uniform on the sphere), longitudes all around; no personal data.");
        sb.AppendLine($"- Sky-only schools: {set.Instants:N0} random instants {set.FromYear}–{set.ToYear}; the person is placed at the birth place of the chart.");
        sb.AppendLine($"- Weather schools: {set.ClimateSamples:N0} random 3-hour slots per climate zone, {set.DataFromYear}–{set.DataToYear} " +
                      "(WMO climate normal), sky and weather from the same real moment at the reference place.");
        sb.AppendLine("- Kp: GFZ Potsdam (CC BY 4.0). Weather: Open-Meteo historical archive, ERA5 (CC BY 4.0). Downloaded into `src/backend/calibration` " +
                      "by `fetch-reference` (gitignored); their content hashes are committed in `calibration/manifest.json`.");
        sb.AppendLine("- Schumann: not in the reference (the feed keeps only 24 hours). Its rule adds raw on top of the reference, so when it fires " +
                      "Biometeorology reads a little rarer than it is; it keeps its fixed threshold until samples are persisted.");
        sb.AppendLine("- A person's place is assigned to the nearest reference place (great-circle distance): an approximation, no climate map is bundled.");
        sb.AppendLine();
        sb.AppendLine("| Climate zone | Köppen | Place |");
        sb.AppendLine("|---|---|---|");
        foreach (var z in ClimateZones.All)
            sb.AppendLine($"| `{z.Id}` | {z.Koppen} | {z.Name}, {z.Latitude.ToString(inv)}, {z.Longitude.ToString(inv)} |");
        sb.AppendLine();
        sb.AppendLine("## Coverage of the cycles");
        sb.AppendLine();
        var skySpan = set.ToYear - set.FromYear + 1;
        var dataSpan = set.DataToYear - set.DataFromYear + 1;
        sb.AppendLine($"| Cycle | Period (years) | Cycles in {skySpan} y (sky schools) | Cycles in {dataSpan} y (weather schools) |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var (name, years) in Cycles)
            sb.AppendLine($"| {name} | {years.ToString("0.##", inv)} | {(skySpan / years).ToString("0.#", inv)} | {(dataSpan / years).ToString("0.#", inv)} |");
        sb.AppendLine();
        sb.AppendLine("Neptune and Pluto are covered only in part by 200 years (accepted). The weather schools read only the yearly and daily " +
                      "cycles of the sky (seasons, light, hours), which 30 years cover many times.");
        sb.AppendLine();
        sb.AppendLine("## Schools");
        sb.AppendLine();
        sb.AppendLine("Per layer: nodes the school covers; nodes it reaches (raw > 0 in at least 1% of the moments); median share of moments " +
                      "at zero (zero stays zero); stability = difference in rarity, in percentile points, between the tables built on two halves " +
                      "of the reference (even and odd charts, half the instants each) at the 50th, 90th, 95th and 99th percentile of the full " +
                      "table: mean and worst node. Doubling the reference roughly divides it by √2.");
        foreach (var s in schools)
        {
            sb.AppendLine();
            sb.AppendLine($"### {s.Name} (`{s.Id}` {s.Version})");
            foreach (var (tableId, t) in results[s.Id])
            {
                sb.AppendLine();
                sb.AppendLine($"Table `{tableId}`, {t.Samples:N0} samples.");
                sb.AppendLine();
                sb.AppendLine("| Layer | Covered | Reached | Median zero share | Stability mean | Stability worst |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (var layer in t.Nodes.GroupBy(n => BodyZones.LayerOf(n.Node)).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var list = layer.ToList();
                    var reached = list.Count(n => n.ZeroShare <= 0.99);
                    var zero = Median(list.Select(n => n.ZeroShare));
                    var worst = list.MaxBy(n => n.Stability)!;
                    sb.AppendLine($"| {layer.Key} | {list.Count} | {reached} | {(zero * 100).ToString("0", inv)}% | " +
                                  $"{list.Average(n => n.Stability).ToString("0.0", inv)} | {worst.Stability.ToString("0.0", inv)} ({worst.Node}) |");
                }
            }
        }
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        return v.Count == 0 ? 0 : v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    private sealed record NodeStats(string Node, double ZeroShare, double Stability);

    private sealed record TableResult(int Samples, SortedDictionary<string, double[]> Quantiles, IReadOnlyList<NodeStats> Nodes)
    {
        public static TableResult From(IReadOnlyList<string> nodes, float[][] raws)
        {
            var quantiles = new SortedDictionary<string, double[]>(StringComparer.Ordinal);
            var stats = new List<NodeStats>();
            for (var n = 0; n < nodes.Count; n++)
            {
                var all = raws[n];
                var even = all.Where((_, i) => i % 2 == 0).ToArray();
                var odd = all.Where((_, i) => i % 2 == 1).ToArray();
                var zeros = all.Count(v => v <= 0) / (double)all.Length;
                var q = NodeQuantiles.Of((float[])all.Clone());
                var qe = NodeQuantiles.Of(even);
                var qo = NodeQuantiles.Of(odd);
                var stability = new[] { 50, 90, 95, 99 }
                    .Max(k => Math.Abs(NodeQuantiles.RarityOf(qe, q[k]) - NodeQuantiles.RarityOf(qo, q[k])) * 100);
                quantiles[nodes[n]] = q;
                stats.Add(new NodeStats(nodes[n], zeros, stability));
            }
            return new TableResult(raws.FirstOrDefault()?.Length ?? 0, quantiles, stats);
        }
    }

    /// <summary>The influences' signals at one reference moment, computed with the same code the API runs.</summary>
    private sealed class SignalSources(IEphemeris eph, IReadOnlyList<EclipseEvent> eclipses)
    {
        private readonly SkySignalGenerator _sky = new(eph);
        private readonly TcmCalendarProvider _tcm = new(eph);
        private readonly HumanDesignProvider _hd = new(eph);

        public IReadOnlyList<Signal> Gather(
            IReadOnlySet<string> needs, DateTimeOffset at, double lat, double lon, NatalChart natal, HumanDesign.Chart hd,
            IReadOnlyDictionary<DateTimeOffset, double>? kp, IReadOnlyDictionary<DateTimeOffset, Hour>? weather)
        {
            var signals = new List<Signal>(128);
            if (needs.Contains(EphemerisProvider.Id)) signals.AddRange(_sky.At(at, new SkyContext(lat, lon, natal), eclipses));
            if (needs.Contains(TcmCalendarProvider.Id)) signals.AddRange(_tcm.At(at, lon));
            if (needs.Contains(HumanDesignProvider.Id)) signals.AddRange(_hd.At(at, hd));
            if (needs.Contains(GfzKpProvider.Influence.Id) && kp is not null && kp.TryGetValue(at, out var k))
                signals.Add(GfzKpProvider.ToSignal(at, k, "def"));
            if (needs.Contains(OpenMeteoWeatherProvider.Influence.Id) && weather is not null)
                signals.AddRange(OpenMeteoWeatherProvider.SignalsAt(OpenMeteoWeatherProvider.Influence.Id, weather, at));
            return signals;
        }
    }
}
