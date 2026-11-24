using System.Diagnostics;
using Dendera.Core.Astro;
using Dendera.Core.Calibration;
using Dendera.Core.Rules;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Api.Services;

/// <summary>
/// Personal reference tables (<see cref="PersonalReference"/>), computed on first use for a birth, a place and a set of
/// schools, then kept in memory. A few seconds the first time; any change to the schools' rules gives a new key.
/// </summary>
public sealed class PersonalReferenceCache(IEphemeris ephemeris, IMemoryCache cache, ILogger<PersonalReferenceCache> log)
{
    public IReadOnlyDictionary<string, NodeQuantiles> For(
        NatalChart natal, IReadOnlyList<RuleSet> schools, NatalBaselines baselines, double latitude, double longitude)
    {
        var personal = schools.Where(PersonalReference.Applies).ToList();
        if (personal.Count == 0) return new Dictionary<string, NodeQuantiles>();
        var b = natal.Birth;
        var key = $"personal-reference:{b.BirthUtc.UtcTicks}:{b.Latitude:0.####}:{b.Longitude:0.####}:{latitude:0.##}:{longitude:0.##}:" +
                  $"{ReferenceSet.SignalsVersion}:{string.Join(",", personal.Select(s => $"{s.Id}={CalibrationHash.Rules(s)}"))}";

        var lazy = cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromDays(1);
            return new Lazy<IReadOnlyDictionary<string, NodeQuantiles>>(() =>
            {
                var watch = Stopwatch.StartNew();
                var tables = PersonalReference.Build(ephemeris, personal, natal,
                    HumanDesign.Compute(ephemeris, b.BirthUtc), baselines, latitude, longitude);
                log.LogInformation("Personal reference for {Schools} built in {Ms} ms",
                    string.Join(", ", personal.Select(s => s.Id)), watch.ElapsedMilliseconds);
                return tables;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        })!;
        return lazy.Value;
    }
}
