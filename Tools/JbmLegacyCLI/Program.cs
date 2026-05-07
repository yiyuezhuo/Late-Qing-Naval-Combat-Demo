using System.Globalization;
using YYZ.Ballistic;

try
{
    Console.WriteLine("JBM LEGACY CLI");
    Console.WriteLine();

    var program = (await Ask("ENTER PROGRAM (MCDRAG, MCGYRO, OR INTLIFT)", "MCDRAG")).ToUpperInvariant();
    if (program.StartsWith("MCG", StringComparison.Ordinal))
        await AskMcGyro(Jbm.DefaultMcGyroInput());
    else if (program.StartsWith("INT", StringComparison.Ordinal))
        await AskIntLift(Jbm.DefaultIntLiftInput());
    else
        await AskMcDrag(Jbm.DefaultMcDragInput());
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static async Task AskMcDrag(McDragInput input)
{
    while (true)
    {
        Console.WriteLine("ENTER THE MCDRAG INPUTS, ONE QUANTITY AT A TIME.");
        input.ReferenceDiameterMm = await AskNumber("ENTER PROJECTILE REFERENCE DIAMETER (MM)", input.ReferenceDiameterMm, 0.001);
        input.TotalLengthCalibers = await AskNumber("ENTER TOTAL PROJECTILE LENGTH (CALIBERS)", input.TotalLengthCalibers, 0.001);
        input.NoseLengthCalibers = await AskNumber("ENTER NOSE LENGTH (CALIBERS)", input.NoseLengthCalibers, 0.001);
        input.TangentRadiusRatio = await AskNumber("ENTER RT/R (HEADSHAPE PARAMETER)", input.TangentRadiusRatio);
        input.BoattailLengthCalibers = await AskNumber("ENTER BOATTAIL LENGTH (CALIBERS)", input.BoattailLengthCalibers, 0);
        input.BaseDiameterCalibers = await AskNumber("ENTER BASE DIAMETER (CALIBERS)", input.BaseDiameterCalibers, 0.001);
        input.MeplatDiameterCalibers = await AskNumber("ENTER MEPLAT DIAMETER (CALIBERS)", input.MeplatDiameterCalibers, 0, 0.999999);
        input.RotatingBandDiameterCalibers = await AskNumber("ENTER ROTATING BAND DIAMETER (CALIBERS)", input.RotatingBandDiameterCalibers, 0.001);
        input.CenterOfGravityCalibers = await AskNumber("ENTER CENTER OF GRAVITY LOCATION (CALIBERS FROM NOSE)", input.CenterOfGravityCalibers);
        input.BoundaryLayer = await AskBoundaryLayer(input.BoundaryLayer);
        input.ProjectileId = await Ask("ENTER PROJECTILE IDENTIFICATION", input.ProjectileId);
        Console.WriteLine();
        WriteLines(Jbm.CalculateMcDrag(input).LegacyReport);
        Console.WriteLine();
        await AskYesNo("COPY THIS", false);
        if (!await AskYesNo("RUN ANOTHER CASE", false))
            break;
    }
}

static async Task AskMcGyro(McGyroInput input)
{
    while (true)
    {
        Console.WriteLine("ENTER THE MCGYRO INPUTS, ONE QUANTITY AT A TIME.");
        input.ReferenceDiameterMm = await AskNumber("ENTER PROJECTILE REFERENCE DIAMETER (MM)", input.ReferenceDiameterMm, 0.001);
        input.TotalLengthCalibers = await AskNumber("ENTER PROJECTILE TOTAL LENGTH (CALIBERS)", input.TotalLengthCalibers, 0.001);
        input.NoseLengthCalibers = await AskNumber("ENTER NOSE LENGTH (CALIBERS)", input.NoseLengthCalibers, 0.001);
        input.TangentRadiusRatio = await AskNumber("ENTER RT/R (HEADSHAPE PARAMETER)", input.TangentRadiusRatio);
        input.BoattailLengthCalibers = await AskNumber("ENTER BOATTAIL LENGTH (CALIBERS)", input.BoattailLengthCalibers, 0);
        input.BaseDiameterCalibers = await AskNumber("ENTER BASE DIAMETER (CALIBERS)", input.BaseDiameterCalibers, 0.001);
        input.MeplatDiameterCalibers = await AskNumber("ENTER MEPLAT DIAMETER (CALIBERS)", input.MeplatDiameterCalibers, 0, 0.999999);
        input.ProjectileDensityGramsPerCc = await AskNumber("ENTER PROJECTILE DENSITY (GRAMS/CC)", input.ProjectileDensityGramsPerCc, 0.001);
        input.RiflingTwistCalibersPerTurn = await AskNumber("ENTER RIFLING TWIST RATE (CALIBERS/TURN)", input.RiflingTwistCalibersPerTurn, 0.001);
        input.ProjectileId = await Ask("ENTER PROJECTILE IDENTIFICATION", input.ProjectileId);
        Console.WriteLine();
        WriteLines(Jbm.CalculateMcGyro(input).LegacyReport);
        Console.WriteLine();
        await AskYesNo("COPY THIS", false);
        if (!await AskYesNo("RUN ANOTHER CASE", false))
            break;
    }
}

static async Task AskIntLift(IntLiftInput input)
{
    while (true)
    {
        Console.WriteLine("INTERIM ESTIMATES OF LIFT, OVERTURNING MOMENT AND YAW DRAG COEFFICIENTS.");
        input.ReferenceDiameterMm = await AskNumber("ENTER REFERENCE DIAMETER (MM)", input.ReferenceDiameterMm, 0.001);
        input.TotalLengthCalibers = await AskNumber("ENTER TOTAL LENGTH (CALIBERS)", input.TotalLengthCalibers, 0.001);
        input.NoseLengthCalibers = await AskNumber("ENTER NOSE LENGTH (CALIBERS)", input.NoseLengthCalibers, 0.001);
        input.TangentRadiusRatio = await AskNumber("ENTER HEADSHAPE PARAMETER (RT/R)", input.TangentRadiusRatio);
        input.BoattailLengthCalibers = await AskNumber("ENTER BOATTAIL LENGTH (CALIBERS)", input.BoattailLengthCalibers, 0);
        input.BaseDiameterCalibers = await AskNumber("ENTER BASE DIAMETER (CALIBERS)", input.BaseDiameterCalibers, 0.001);
        input.MeplatDiameterCalibers = await AskNumber("ENTER MEPLAT DIAMETER (CALIBERS)", input.MeplatDiameterCalibers, 0, 0.999999);
        input.CenterOfGravityCalibers = await AskNumber("ENTER CENTER OF GRAVITY (CALIBERS FROM NOSE)", input.CenterOfGravityCalibers);
        input.ProjectileId = await Ask("ENTER PROJECTILE IDENTIFICATION", input.ProjectileId);
        Console.WriteLine();
        WriteLines(Jbm.CalculateIntLift(input).LegacyReport);
        Console.WriteLine();
        await AskYesNo("COPY THIS OUTPUT", false);
        if (!await AskYesNo("DO YOU WANT TO RUN ANOTHER CASE", false))
            break;
    }
}

static async Task<string> AskBoundaryLayer(string fallback)
{
    while (true)
    {
        var code = (await Ask("ENTER THE BOUNDARY LAYER CODE (L/L, L/T, OR T/T)", fallback)).ToUpperInvariant();
        if (code == JbmBoundaryLayer.LaminarLaminar || code == JbmBoundaryLayer.LaminarTurbulent || code == JbmBoundaryLayer.TurbulentTurbulent)
            return code;
        Console.WriteLine("INCORRECT BOUNDARY LAYER CODE. PLEASE TRY AGAIN.");
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
