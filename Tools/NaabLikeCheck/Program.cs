using NavalCombatCore;

var data = NaabLikeBallisticsData.LoadEmbedded();
var bismarck = new NaabLikeProjectile
{
    name = "38cm/47 SK C/34 APC#",
    diameterInches = 14.96f,
    totalWeightPounds = 1763.70f,
    bodyWeightPounds = 1552.05f,
    windscreenWeightPounds = 52.91f,
    apCapWeightPounds = 158.74f,
    hcwclcrCapType = 1,
    windscreenNblAddendMultiplier = 0.33f,
    highObliquityWindscreenNblAddendMultiplier = 0.1f,
    highObliquityThresholdDeg = 0f,
    muzzleVelocityFeetPerSecond = 2690f,
    maxRangeYards = 38870f,
    dragFunction = NaabLikeDragFunction.G7,
    ballisticCoefficient = 7.7734f,
    dragCoefficientAdjust = 0f,
    maxElevationDeg = 30f,
    effectiveShellQuality = 0.985f
};

float? angleHint = null;
Run(bismarck, 1000f, ref angleHint);
Run(bismarck, 10000f, ref angleHint);

void Run(NaabLikeProjectile projectile, float rangeYards, ref float? angleHintDeg)
{
    var exterior = new NaabLikeExteriorBallisticsSolver(data.dragTables[projectile.dragFunction], projectile);
    var hit = exterior.SolveForTargetRange(rangeYards, MathF.Max(projectile.maxElevationDeg, 45f), angleHintDeg);
    if (!hit.success)
    {
        Console.WriteLine($"{projectile.name} range={rangeYards:0} no solution");
        return;
    }

    angleHintDeg = hit.elevationDeg;
    var armor = new NaabLikeArmorInput
    {
        quality = 0.95f,
        elongationPercent = 22f,
        bhn = 235f,
        inclinedDeg = 0f
    };
    var terminal = new NaabLikeTerminalBallisticsSolver(data.terminalTables, projectile, armor);
    var sideObliquityDeg = armor.inclinedDeg + hit.angleOfFallDeg;
    var deckObliquityDeg = armor.inclinedDeg + MathF.Max(90f - hit.angleOfFallDeg, 0f);
    var vertical = terminal.CompletePenetrationInches(hit.impactVelocityFeetPerSecond, sideObliquityDeg);
    var horizontal = terminal.CompletePenetrationInches(hit.impactVelocityFeetPerSecond, deckObliquityDeg);

    Console.WriteLine(
        $"{projectile.name} range={rangeYards:0} elev={hit.elevationDeg:0.000} v={hit.impactVelocityFeetPerSecond:0} " +
        $"fall={hit.angleOfFallDeg:0.00} deck={deckObliquityDeg:0.00} horizontal={horizontal:0.00} vertical={vertical:0.00}");
}
