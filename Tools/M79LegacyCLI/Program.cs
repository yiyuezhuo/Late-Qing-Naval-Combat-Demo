using System.Globalization;
using YYZ.Ballistic;

try
{
    Console.WriteLine("M79 APCLC LEGACY CLI");
    Console.WriteLine();

    var mode = (await Ask("ENTER P FOR SINGLE PENETRATION OR R FOR RANGE TABLE", "P")).ToUpperInvariant();
    if (mode.StartsWith("R", StringComparison.Ordinal))
        await InteractiveRange(M79.DefaultInput());
    else
        await InteractiveSingle(M79.DefaultInput());
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static async Task InteractiveSingle(M79Input input)
{
    Console.WriteLine("NAVY BALLISTIC LIMIT, ENERGY, EXIT ANGLE, & REMAINING VELOCITY FOR M79-LIKE AP SHOT");
    Console.WriteLine("Press RETURN with no entry to repeat previously entered value.");
    while (true)
    {
        input.ProjectileDiameter = await AskNumber("Projectile Diameter (Inches)", input.ProjectileDiameter, 0.001);
        input.ProjectileWeight = await AskNumber("Projectile Total Weight (Pounds)", input.ProjectileWeight, 0.001);
        input.PlateThickness = await AskNumber("Plate Thickness (Inches)", input.PlateThickness, input.ProjectileDiameter * 0.001, input.ProjectileDiameter * 6);
        input.PlateQuality = await AskNumber("Plate Quality Factor (Dimensionless)", input.PlateQuality, 0.001);
        input.Obliquity = await AskNumber("Obliquity (Degrees) (80 Degrees Maximum)", input.Obliquity, 0, 80);
        input.StrikingVelocity = await AskNumber("Striking Velocity (Feet/Second) (3500 Feet/Second Maximum)", input.StrikingVelocity, 0.001, 3500);
        input.Elongation = await AskNumber("Percent Elongation of Armor Metal Used (10% Minimum)", input.Elongation, 10);
        Console.WriteLine();
        WriteLines(M79.Calculate(input).LegacyReport);
        Console.WriteLine();
        await AskYesNo("Print Copy of Results on Printer", false);
        await AskYesNo("Print A Header-Only Introduction Page on Printer", false);
        if (!await AskYesNo("Another Run", false))
            break;
        Console.WriteLine();
    }
}

static async Task InteractiveRange(M79Input baseInput)
{
    while (true)
    {
        baseInput.ProjectileDiameter = await AskNumber("Projectile Diameter (Inches)", baseInput.ProjectileDiameter, 0.001);
        baseInput.ProjectileWeight = await AskNumber("Projectile Total Weight (Pounds)", baseInput.ProjectileWeight, 0.001);
        var minPlateThickness = await AskNumber("Minimum Plate Thickness (Inches)", baseInput.PlateThickness, baseInput.ProjectileDiameter * 0.001, baseInput.ProjectileDiameter * 6);
        var maxPlateThickness = await AskNumber("Maximum Plate Thickness (Inches)", minPlateThickness, minPlateThickness, baseInput.ProjectileDiameter * 6);
        var thicknessStep = await AskNumber("Thickness Step (Inches) (0.0001 minimum, if not zero)", Math.Max(baseInput.ProjectileDiameter * 0.1, 0.0001), 0);
        baseInput.PlateQuality = await AskNumber("Plate Quality Factor (Dimensionless)", baseInput.PlateQuality, 0.001);
        baseInput.Elongation = await AskNumber("Plate Percent Elongation (10% Minimum)", baseInput.Elongation, 10);
        baseInput.Obliquity = await AskNumber("Obliquity (Degrees) (80 Degrees Maximum)", baseInput.Obliquity, 0, 80);

        var rows = M79.ScanLegacyRange(new M79RangeInput
        {
            ProjectileDiameter = baseInput.ProjectileDiameter,
            ProjectileWeight = baseInput.ProjectileWeight,
            MinPlateThickness = minPlateThickness,
            MaxPlateThickness = maxPlateThickness,
            ThicknessStep = thicknessStep,
            PlateQuality = baseInput.PlateQuality,
            Elongation = baseInput.Elongation,
            Obliquity = baseInput.Obliquity
        });

        Console.WriteLine("    T     T/D       NBL    ENERGY    ECOSOB^2     NBLNF   ENRGNF    ENFCOB^2");
        foreach (var row in rows)
        {
            var nfn = Math.Abs(row.NoseFirstNbl - row.Nbl) <= 0.1 ? "=NBL" : row.NoseFirstNbl.ToString(CultureInfo.InvariantCulture);
            var enf = Math.Abs(row.NoseFirstNbl - row.Nbl) <= 0.1 ? "=ENRGY" : row.NoseFirstEnergy.ToString(CultureInfo.InvariantCulture);
            var enc = Math.Abs(row.NoseFirstNbl - row.Nbl) <= 0.1 ? "=ECSOB^2" : row.NoseFirstNormalEnergy.ToString(CultureInfo.InvariantCulture);
            Console.WriteLine($"{row.Thickness.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(8)} {row.TSlashD.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(8)} {row.Nbl.ToString(CultureInfo.InvariantCulture).PadLeft(6)} {row.Energy.ToString(CultureInfo.InvariantCulture).PadLeft(9)} {row.NormalEnergy.ToString(CultureInfo.InvariantCulture).PadLeft(10)} {nfn.PadLeft(8)} {enf.PadLeft(9)} {enc.PadLeft(10)}");
        }

        var choice = (await Ask("Another run? (Y=Yes/N=No/P=Yes but only projectile & run data)", "N")).ToUpperInvariant();
        if (choice == "N")
            break;
    }
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

static async Task<double> AskNumber(string prompt, double fallback, double? min = null, double? max = null)
{
    while (true)
    {
        var raw = await Ask(prompt, fallback.ToString(CultureInfo.InvariantCulture));
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && (min == null || value >= min)
            && (max == null || value <= max))
        {
            return value;
        }
        Console.WriteLine($"Enter a number{(min == null ? "" : " >= " + min.Value.ToString(CultureInfo.InvariantCulture))}{(max == null ? "" : " <= " + max.Value.ToString(CultureInfo.InvariantCulture))}.");
    }
}

static void WriteLines(IEnumerable<string> lines)
{
    foreach (var line in lines)
        Console.WriteLine(line);
}
