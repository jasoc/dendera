using Dendera.Core.Signals;
using Dendera.Sources;

namespace Dendera.Core.Tests;

/// <summary>The influences registered in Dendera.Api/Program.cs, for validating school files in tests.</summary>
internal static class Influences
{
    public static readonly IReadOnlyList<InfluenceInfo> All =
    [
        EphemerisProvider.Influence,
        TcmCalendarProvider.Influence,
        HumanDesignProvider.Influence,
        GfzKpProvider.Influence,
        OpenMeteoWeatherProvider.Influence,
        SchumannProvider.Influence,
    ];
}
