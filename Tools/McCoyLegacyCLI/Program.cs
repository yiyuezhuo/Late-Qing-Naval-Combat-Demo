using System.Globalization;
using YYZ.Ballistic;

try
{
    Console.WriteLine("MCCOY EXTERIOR BALLISTICS LEGACY CLI");
    Console.WriteLine();
    await Interactive(McCoy.DefaultInput());
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static async Task Interactive(McCoyInput initial)
{
    var input = Clone(initial);
    while (true)
    {
        input.DragName = await Ask("ENTER DRAG FUNCTION TO BE USED", input.DragName);
        input.DragTable = await AskDragTable(input.DragTable.Count >= 2 ? input.DragTable : McCoy.DefaultDragTable());
        if (!await AskYesNo("IS THE ABOVE DRAG TABLE CORRECT", true))
            continue;

        input.RangeUnit = (await Ask("RANGE IN YARDS OR METERS? (ENTER Y FOR YARDS, M FOR METERS)", input.RangeUnit == McCoyRangeUnit.Yards ? "Y" : "M"))
            .ToUpperInvariant()
            .StartsWith("M", StringComparison.Ordinal)
            ? McCoyRangeUnit.Meters
            : McCoyRangeUnit.Yards;
        input.Atmosphere = (await Ask("ARMY STANDARD METRO OR ICAO? (ENTER S FOR STANDARD, I FOR ICAO)", input.Atmosphere == McCoyAtmosphere.StandardMetro ? "S" : "I"))
            .ToUpperInvariant()
            .StartsWith("I", StringComparison.Ordinal)
            ? McCoyAtmosphere.Icao
            : McCoyAtmosphere.StandardMetro;
        input.ProjectileId = await Ask("ENTER PROJECTILE IDENTIFICATION", input.ProjectileId);
        input.MuzzleVelocity = await AskNumber("ENTER MUZZLE VELOCITY (FEET/SECOND)", input.MuzzleVelocity, 1);
        input.BallisticCoefficient = await AskNumber("ENTER BALLISTIC COEFFICIENT (LB/IN 2)", input.BallisticCoefficient, 0.0001);
        input.SightHeight = await AskNumber("ENTER HEIGHT OF SIGHT LINE ABOVE BORE LINE (INCHES)", input.SightHeight);
        input.ElevationMinutes = await AskNumber("ENTER GUN ELEVATION ANGLE (MINUTES)", input.ElevationMinutes);
        input.DensityRatio = await AskNumber("ENTER RATIO OF AIR DENSITY TO SEA LEVEL STANDARD", input.DensityRatio, 0.0001);
        input.TemperatureF = await AskNumber("ENTER AIR TEMPERATURE (DEGREES, FAHRENHEIT)", input.TemperatureF);
        input.PrintInterval = await AskNumber($"ENTER RANGE PRINT INTERVAL ({BallisticOptions.ToLegacyCode(input.RangeUnit).ToUpperInvariant()})", input.PrintInterval, 1);
        input.MaxRange = await AskNumber($"ENTER RANGE TO TERMINATE TRAJECTORY ({BallisticOptions.ToLegacyCode(input.RangeUnit).ToUpperInvariant()})", input.MaxRange, 1);
        input.RangeWindMph = await AskNumber("ENTER RANGE WIND SPEED (MILES/HOUR)", input.RangeWindMph);
        input.CrossWindMph = await AskNumber("ENTER CROSS WIND SPEED (MILES/HOUR)", input.CrossWindMph);
        input.MatchRange = await AskNumber($"ENTER THE TRAJECTORY MATCH RANGE, RMATCH ({BallisticOptions.ToLegacyCode(input.RangeUnit).ToUpperInvariant()})", input.MatchRange, 0);
        input.MatchHeight = await AskNumber("ENTER THE TRAJECTORY MATCH HEIGHT, HMATCH (INCHES)", input.MatchHeight);

        if (Math.Floor(input.MaxRange / input.PrintInterval + 1.5) > 101
            && await AskYesNo("THIS PRINT INTERVAL GIVES OVER 100 LINES OF OUTPUT. INCREASE PRINT STEP", true))
        {
            continue;
        }

        Console.WriteLine();
        WriteLines(McCoy.Calculate(input).LegacyReport);
        Console.WriteLine();
        await AskYesNo("DO YOU WANT HARD COPY OF THIS OUTPUT", false);
        if (!await AskYesNo("RUN ANOTHER CASE", false))
            break;
        if (!await AskYesNo("USE SAME DRAG COEFFICIENT TABLE", true))
            input.DragTable = new List<DragPoint>();
    }
}

static async Task<List<DragPoint>> AskDragTable(List<DragPoint> fallback)
{
    Console.WriteLine("THE DRAG COEFFICIENT IS ENTERED AS A TABLE OF MACH NUMBER (M) VERSUS CD.");
    Console.WriteLine("Enter blank at the first prompt to reuse the current table.");
    Console.WriteLine(McCoy.DragTableToText(fallback));

    Console.Write("ENTER M, CD: ");
    var first = (await Console.In.ReadLineAsync())?.Trim();
    if (string.IsNullOrEmpty(first))
        return fallback;

    var lines = new List<string> { first };
    while (true)
    {
        Console.Write("ENTER M, CD (negative Mach ends table): ");
        var line = (await Console.In.ReadLineAsync())?.Trim() ?? "";
        lines.Add(line);
        var parts = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mach)
            && mach < 0)
        {
            break;
        }
    }

    var lastParts = lines[^1].Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    if (lastParts.Length >= 2 && double.TryParse(lastParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lastMach))
        lines[^1] = $"{Math.Abs(lastMach).ToString(CultureInfo.InvariantCulture)}, {lastParts[1]}";

    var parsed = McCoy.ParseDragTable(string.Join("\n", lines));
    return parsed.Count >= 2 ? parsed : fallback;
}

static McCoyInput Clone(McCoyInput source)
{
    return new McCoyInput
    {
        DragName = source.DragName,
        DragTable = source.DragTable.Select(point => new DragPoint { Mach = point.Mach, Cd = point.Cd }).ToList(),
        RangeUnit = source.RangeUnit,
        Atmosphere = source.Atmosphere,
        ProjectileId = source.ProjectileId,
        MuzzleVelocity = source.MuzzleVelocity,
        BallisticCoefficient = source.BallisticCoefficient,
        SightHeight = source.SightHeight,
        ElevationMinutes = source.ElevationMinutes,
        DensityRatio = source.DensityRatio,
        TemperatureF = source.TemperatureF,
        PrintInterval = source.PrintInterval,
        MaxRange = source.MaxRange,
        RangeWindMph = source.RangeWindMph,
        CrossWindMph = source.CrossWindMph,
        MatchRange = source.MatchRange,
        MatchHeight = source.MatchHeight
    };
}

static async Task<bool> AskYesNo(string prompt, bool fallback)
{
    while (true)
    {
        var raw = (await Ask(prompt, fallback ? "Y" : "N")).ToUpperInvariant();
        if (raw == "Y" || raw == "YES")
            return true;
        if (raw == "N" || raw == "NO")
            return false;
        Console.WriteLine("Enter Y or N.");
    }
}

static async Task<string> Ask(string prompt, string fallback)
{
    Console.Write($"{prompt} [{fallback}]: ");
    var answer = (await Console.In.ReadLineAsync())?.Trim();
    return string.IsNullOrEmpty(answer) ? fallback : answer;
}

static async Task<double> AskNumber(string prompt, double fallback, double? min = null)
{
    while (true)
    {
        var raw = await Ask(prompt, fallback.ToString(CultureInfo.InvariantCulture));
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && (min == null || value >= min))
            return value;
        Console.WriteLine($"Enter a number{(min == null ? "" : " >= " + min.Value.ToString(CultureInfo.InvariantCulture))}.");
    }
}

static void WriteLines(IEnumerable<string> lines)
{
    foreach (var line in lines)
        Console.WriteLine(line);
}
