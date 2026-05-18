using System.Globalization;
using System.Xml.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

return Run(args);

static int Run(string[] args)
{
    try
    {
        var options = Options.Parse(args);
        if (options.showHelp)
        {
            PrintUsage();
            return 0;
        }

        var shipElements = LoadShipElements(options.scenarioPath);
        var selectedShips = SelectShips(shipElements, options).ToList();
        if (selectedShips.Count == 0)
        {
            Console.Error.WriteLine("No matching ship classes found.");
            return 1;
        }

        Directory.CreateDirectory(options.outputDirectory);
        var usedBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var shipElement in selectedShips)
        {
            var input = ParseShip(shipElement);
            if (input.type == PlaceholderShipType.LandBattery)
            {
                Console.WriteLine($"Skipped {input.name}: Land Battery does not support placeholder ship images.");
                continue;
            }

            if (input.lengthFoot <= 0 || input.beamFoot <= 0)
            {
                Console.WriteLine($"Skipped {input.name}: lengthFoot and beamFoot must both be greater than 0.");
                continue;
            }

            var settings = BuildSettings(input, options);
            var baseName = MakeUniqueBaseName(SanitizeFileName(input.name), usedBaseNames);
            WriteRenderedImages(input, settings, options.outputDirectory, baseName);
            Console.WriteLine($"Generated {input.name} ({settings.canvasWidth}x{settings.canvasHeight}) -> {options.outputDirectory}");
        }

        return 0;
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine();
        PrintUsage();
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
        return 1;
    }
}

static IReadOnlyList<XElement> LoadShipElements(string scenarioPath)
{
    if (!File.Exists(scenarioPath))
        throw new FileNotFoundException($"ShipClasses XML was not found: {scenarioPath}", scenarioPath);

    var document = XDocument.Load(scenarioPath);
    return document.Root?.Elements("ShipClass").ToList() ?? new List<XElement>();
}

static IEnumerable<XElement> SelectShips(IEnumerable<XElement> ships, Options options)
{
    if (options.allPlaceholders)
        return ships.Where(ship => BoolValue(ship, "isGraphicPlaceholder"));

    if (!string.IsNullOrWhiteSpace(options.objectId))
        return ships.Where(ship => string.Equals(TextValue(ship, "objectId"), options.objectId, StringComparison.OrdinalIgnoreCase));

    if (!string.IsNullOrWhiteSpace(options.name))
    {
        var exact = ships
            .Where(ship => string.Equals(ShipName(ship), options.name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count > 0)
            return exact;

        return ships.Where(ship => ShipName(ship).Contains(options.name, StringComparison.OrdinalIgnoreCase));
    }

    throw new ArgumentException("Select a ship with --name, --id, or --all-placeholders.");
}

static ShipClassPlaceholderRenderInput ParseShip(XElement ship)
{
    return new ShipClassPlaceholderRenderInput
    {
        name = ShipName(ship),
        type = EnumValue(TextValue(ship, "type"), PlaceholderShipType.NotSpecified),
        displacementTons = FloatValue(ship, "displacementTons"),
        lengthFoot = FloatValue(ship, "lengthFoot"),
        beamFoot = FloatValue(ship, "beamFoot"),
        speedKnots = FloatValue(ship, "speedKnots"),
        batteryRecords = ParseBatteryRecords(ship.Element("batteryRecords")).ToList(),
        torpedoMountLocationRecords = ParseMountRecords(ship.Element("torpedoSector")?.Element("mountLocationRecords")).ToList(),
        rapidFireBatteryRecords = ParseRapidFireRecords(ship.Element("rapidFireBatteryRecords")).ToList(),
    };
}

static IEnumerable<PlaceholderBatteryRenderRecord> ParseBatteryRecords(XElement container)
{
    foreach (var battery in container?.Elements("BatteryRecord") ?? Enumerable.Empty<XElement>())
    {
        yield return new PlaceholderBatteryRenderRecord
        {
            shellSizeInch = FloatValue(battery, "shellSizeInch"),
            mountLocationRecords = ParseMountRecords(battery.Element("mountLocationRecords")).ToList()
        };
    }
}

static IEnumerable<PlaceholderMountRenderRecord> ParseMountRecords(XElement container)
{
    foreach (var mount in container?.Elements("MountLocationRecord") ?? Enumerable.Empty<XElement>())
    {
        yield return new PlaceholderMountRenderRecord
        {
            mountLocation = EnumValue(TextValue(mount, "mountLocation"), PlaceholderMountLocation.NotSpecified),
            barrels = IntValue(mount, "barrels"),
            mounts = IntValue(mount, "mounts"),
            trainable = BoolValue(mount, "trainable"),
            mountArcs = ParseMountArcs(mount.Element("mountArcs")).ToList()
        };
    }
}

static IEnumerable<PlaceholderMountArcRenderRecord> ParseMountArcs(XElement container)
{
    foreach (var arc in container?.Elements("MountArcRecord") ?? Enumerable.Empty<XElement>())
    {
        yield return new PlaceholderMountArcRenderRecord
        {
            startDeg = FloatAttribute(arc, "startDeg"),
            CoverageDeg = FloatAttribute(arc, "CoverageDeg"),
            isCrossDeckFire = BoolAttribute(arc, "isCrossDeckFire")
        };
    }
}

static IEnumerable<PlaceholderRapidFireRenderRecord> ParseRapidFireRecords(XElement container)
{
    foreach (var record in container?.Elements("RapidFireBatteryRecord") ?? Enumerable.Empty<XElement>())
    {
        yield return new PlaceholderRapidFireRenderRecord
        {
            barrelsLevelPort = ParseIntList(record.Element("barrelsLevelPort")).ToList(),
            barrelsLevelStarboard = ParseIntList(record.Element("barrelsLevelStarboard")).ToList(),
        };
    }
}

static IEnumerable<int> ParseIntList(XElement container)
{
    foreach (var item in container?.Elements("int") ?? Enumerable.Empty<XElement>())
    {
        if (int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            yield return value;
    }
}

static ShipClassPlaceholderImageRenderSettings BuildSettings(ShipClassPlaceholderRenderInput input, Options options)
{
    var settings = new ShipClassPlaceholderImageRenderSettings
    {
        canvasWidth = ClampInt(options.canvasWidth ?? 1200, 320, 4096),
        canvasHeight = ClampInt(options.canvasHeight ?? 250, 96, 2048),
        hullPadding = ClampInt(options.hullPadding ?? 18, 4, 256),
        lineWidth = ClampInt(options.lineWidth ?? 3, 1, 24),
        deckInsetAmount = ClampInt(options.deckInsetAmount ?? 10, 2, 80),
        superstructureHeightScale = ClampFloat(options.superstructureHeightScale ?? 1f, 0.4f, 2.5f),
        funnelCountMode = options.funnelCountMode ?? PlaceholderFunnelCountMode.Auto,
        funnelSpacingBias = ClampFloat(options.funnelSpacingBias ?? 0f, -0.35f, 0.35f),
        bowSharpness = ClampFloat(options.bowSharpness ?? 1.15f, 0.45f, 2.5f),
        sternFullness = ClampFloat(options.sternFullness ?? 1.05f, 0.45f, 2.5f),
        weaponScale = ClampFloat(options.weaponScale ?? 1f, 0.4f, 3f),
    };

    if (!options.canvasWidth.HasValue)
        settings.canvasWidth = ClampInt(ShipClassPlaceholderImageRendererCore.CalculateRecommendedCanvasWidth(input, settings), 320, 4096);

    return settings;
}

static void WriteRenderedImages(ShipClassPlaceholderRenderInput input, ShipClassPlaceholderImageRenderSettings settings, string outputDirectory, string baseName)
{
    WritePng(
        ShipClassPlaceholderImageRendererCore.Render(input, settings, ShipClassPlaceholderImageRendererCore.RenderVariant.Preview),
        Path.Combine(outputDirectory, $"{baseName}_Preview.png"));
    WriteJpeg(
        ShipClassPlaceholderImageRendererCore.Render(input, settings, ShipClassPlaceholderImageRendererCore.RenderVariant.TopJpg),
        Path.Combine(outputDirectory, $"{baseName}_Top.jpg"));
    WritePng(
        ShipClassPlaceholderImageRendererCore.Render(input, settings, ShipClassPlaceholderImageRendererCore.RenderVariant.IconPng),
        Path.Combine(outputDirectory, $"{baseName}_Icon.png"));
}

static void WritePng(RgbaImage source, string path)
{
    using var image = Image.LoadPixelData<Rgba32>(source.pixels, source.width, source.height);
    image.SaveAsPng(path, new PngEncoder());
}

static void WriteJpeg(RgbaImage source, string path)
{
    using var image = Image.LoadPixelData<Rgba32>(source.pixels, source.width, source.height);
    image.SaveAsJpeg(path, new JpegEncoder { Quality = 90 });
}

static string ShipName(XElement ship)
{
    return ship.Element("name")?.Element("english")?.Value?.Trim()
        ?? TextValue(ship, "objectId")
        ?? "ShipClass";
}

static string TextValue(XElement element, string childName)
{
    return element?.Element(childName)?.Value?.Trim();
}

static int IntValue(XElement element, string childName)
{
    return int.TryParse(TextValue(element, childName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value
        : 0;
}

static float FloatValue(XElement element, string childName)
{
    return float.TryParse(TextValue(element, childName), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        ? value
        : 0f;
}

static bool BoolValue(XElement element, string childName)
{
    return bool.TryParse(TextValue(element, childName), out var value) && value;
}

static float FloatAttribute(XElement element, string attributeName)
{
    return float.TryParse(element?.Attribute(attributeName)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        ? value
        : 0f;
}

static bool BoolAttribute(XElement element, string attributeName)
{
    return bool.TryParse(element?.Attribute(attributeName)?.Value, out var value) && value;
}

static T EnumValue<T>(string value, T fallback) where T : struct
{
    return Enum.TryParse(value, out T parsed) ? parsed : fallback;
}

static string SanitizeFileName(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var chars = string.IsNullOrWhiteSpace(value) ? "ShipClass".ToCharArray() : value.ToCharArray();
    for (var i = 0; i < chars.Length; i++)
    {
        if (invalid.Contains(chars[i]))
            chars[i] = '_';
    }

    return new string(chars).Trim();
}

static string MakeUniqueBaseName(string baseName, HashSet<string> usedBaseNames)
{
    if (usedBaseNames.Add(baseName))
        return baseName;

    for (var i = 2; ; i++)
    {
        var candidate = $"{baseName}_{i}";
        if (usedBaseNames.Add(candidate))
            return candidate;
    }
}

static int ClampInt(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
static float ClampFloat(float value, float min, float max) => MathF.Min(MathF.Max(value, min), max);

static void PrintUsage()
{
    Console.WriteLine("""
Usage:
  dotnet run --project Tools/PlaceholderImageCli -- --name "Yoshino"
  dotnet run --project Tools/PlaceholderImageCli -- --all-placeholders --out .codex-tmp/placeholder-preview

Options:
  --scenario <path>             ShipClasses XML path. Defaults to Assets/StreamingAssets/Scenarios/ShipClasses.xml.
  --out <dir>                   Output directory. Defaults to .codex-tmp/placeholder-preview.
  --name <text>                 Match by English ship class name. Exact match first, then contains match.
  --id <objectId>               Match by ShipClass objectId.
  --all-placeholders            Render all ship classes marked isGraphicPlaceholder.
  --canvas-width <px>           Fixed canvas width. Omit to use the recommended width.
  --canvas-height <px>          Canvas height.
  --hull-padding <px>
  --line-width <px>
  --deck-inset <px>
  --superstructure-scale <n>
  --funnel-count <auto|zero|one|two|three>
  --funnel-spacing-bias <n>
  --bow-sharpness <n>
  --stern-fullness <n>
  --weapon-scale <n>
""");
}

sealed class Options
{
    public string scenarioPath = Path.Combine("Assets", "StreamingAssets", "Scenarios", "ShipClasses.xml");
    public string outputDirectory = Path.Combine(".codex-tmp", "placeholder-preview");
    public string name;
    public string objectId;
    public bool allPlaceholders;
    public bool showHelp;
    public int? canvasWidth;
    public int? canvasHeight;
    public int? hullPadding;
    public int? lineWidth;
    public int? deckInsetAmount;
    public float? superstructureHeightScale;
    public PlaceholderFunnelCountMode? funnelCountMode;
    public float? funnelSpacingBias;
    public float? bowSharpness;
    public float? sternFullness;
    public float? weaponScale;

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.showHelp = true;
                    break;
                case "--scenario":
                    options.scenarioPath = NeedValue(args, ref i, arg);
                    break;
                case "--out":
                    options.outputDirectory = NeedValue(args, ref i, arg);
                    break;
                case "--name":
                    options.name = NeedValue(args, ref i, arg);
                    break;
                case "--id":
                    options.objectId = NeedValue(args, ref i, arg);
                    break;
                case "--all-placeholders":
                    options.allPlaceholders = true;
                    break;
                case "--canvas-width":
                    options.canvasWidth = ParseInt(NeedValue(args, ref i, arg), arg);
                    break;
                case "--canvas-height":
                    options.canvasHeight = ParseInt(NeedValue(args, ref i, arg), arg);
                    break;
                case "--hull-padding":
                    options.hullPadding = ParseInt(NeedValue(args, ref i, arg), arg);
                    break;
                case "--line-width":
                    options.lineWidth = ParseInt(NeedValue(args, ref i, arg), arg);
                    break;
                case "--deck-inset":
                    options.deckInsetAmount = ParseInt(NeedValue(args, ref i, arg), arg);
                    break;
                case "--superstructure-scale":
                    options.superstructureHeightScale = ParseFloat(NeedValue(args, ref i, arg), arg);
                    break;
                case "--funnel-count":
                    options.funnelCountMode = ParseFunnelCountMode(NeedValue(args, ref i, arg));
                    break;
                case "--funnel-spacing-bias":
                    options.funnelSpacingBias = ParseFloat(NeedValue(args, ref i, arg), arg);
                    break;
                case "--bow-sharpness":
                    options.bowSharpness = ParseFloat(NeedValue(args, ref i, arg), arg);
                    break;
                case "--stern-fullness":
                    options.sternFullness = ParseFloat(NeedValue(args, ref i, arg), arg);
                    break;
                case "--weapon-scale":
                    options.weaponScale = ParseFloat(NeedValue(args, ref i, arg), arg);
                    break;
                default:
                    if (!arg.StartsWith("--", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(options.name))
                        options.name = arg;
                    else
                        throw new ArgumentException($"Unknown argument: {arg}");
                    break;
            }
        }

        return options;
    }

    static string NeedValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{optionName} requires a value.");
        index++;
        return args[index];
    }

    static int ParseInt(string value, string optionName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        throw new ArgumentException($"{optionName} expects an integer value.");
    }

    static float ParseFloat(string value, string optionName)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        throw new ArgumentException($"{optionName} expects a numeric value.");
    }

    static PlaceholderFunnelCountMode ParseFunnelCountMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "auto" => PlaceholderFunnelCountMode.Auto,
            "zero" or "0" => PlaceholderFunnelCountMode.Zero,
            "one" or "1" => PlaceholderFunnelCountMode.One,
            "two" or "2" => PlaceholderFunnelCountMode.Two,
            "three" or "3" => PlaceholderFunnelCountMode.Three,
            _ => throw new ArgumentException("--funnel-count expects auto, zero, one, two, or three.")
        };
    }
}
