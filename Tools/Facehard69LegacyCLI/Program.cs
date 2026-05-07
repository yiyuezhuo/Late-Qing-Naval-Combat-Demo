using System.Globalization;
using YYZ.Ballistic;

var nationNames = new[]
{
    "United States",
    "Britain",
    "Germany",
    "France",
    "Italy",
    "Japan",
    "Austria-Hungary",
    "Russia"
};

var armorNames = new[]
{
    "Gruson Chilled Cast Iron (1868-90) (land fortification dome turrets)",
    "Average Compound (hardened-steel-faced wrought iron) (1880-90)",
    "Harveyized Mild Steel (1891-1900)",
    "Harveyized Nickel-Steel (1890-1900) (usual 'Harvey' armor)",
    "German original Krupp Cemented (1894-1918) ('KC a/A')",
    "German new KC n/A (1928-36) for Pocket Battleship turrets only",
    "German improved thick-plate KC n/A (1936-45) (SCHARNHORST & BISMARCK)",
    "Austro-Hungarian Witkowitz KC-type Armor (1898-1918)",
    "British average KC-type Armor manufactured 1911-21",
    "British average KC-type Armor manufactured 1922-30",
    "British average post-1930 Cemented Armor (CA)",
    "Italian Terni Cemented Armor (TC) (1935-45)",
    "Japanese Vickers Hardened Armor (VH) (1937-45)",
    "U.S. Midvale Non-Cemented Class 'A' (1907-12 & one cemented Lot in 1922)",
    "U.S. Bethlehem Thin Chill Class 'A' (1921-25)",
    "U.S. average of all other Class 'A' manufactured 1911-25",
    "U.S. average 1935-1943 Class 'A'",
    "U.S. average 1944-1950 Class 'A' (improved)",
    "Average of all other KC introduced before 1911",
    "Average of all other KC introduced between 1911 and 1921",
    "Average of all other KC introduced between 1922 and 1930",
    "Average of all other KC introduced after 1930"
};

var projectileMenus = new[]
{
    new[]
    {
        "Ave. Army/Navy Chilled Cast Iron Shot & all Common Shell (1890-1910)",
        "Ave. capped Chilled Cast Iron Army Coast Defense Shot/Shell",
        "A/N Steel AP Shot/Shell (1890-1910)",
        "A/N soft-capped Steel APC Shot/Shell",
        "Midvale tough-steel AP Shot/Shell",
        "Midvale tough soft-capped APC Shot/Shell",
        "Base-fuzed 7/12/14-in Bombardment light-case shell",
        "Ave. Navy 1911-23 APC except Midvale 8-in Mk 11",
        "Navy Midvale 8-in Mk 11 & Midvale Unbreakable 1916 APC",
        "Average 1921-1935 ACD APC Shot",
        "Average post-1935 ACD APC Shot",
        "Ave WWI-era base-fuzed Common",
        "Ave Special Common with Hood/windscreen/base fuze",
        "6-in Mk 27 Special Common",
        "8-in Mk 15 hard-capped Special Common",
        "3-in Mk 29/30 and 8-in Mk 19 early APC",
        "8-in Mk 19-4-6 APC",
        "6-in Mk 35 and 16-in Mk 8 early APC",
        "8-in Mk 21, 14-in Mk 16, 16-in Mk 5 APC",
        "Late 3/6/8/12/14/16-in APC"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Average Steel AP Shot/Shell",
        "Average uncapped Common, Pointed (CP)",
        "Average Common, Pointed, Capped (CPC)",
        "6 to 12-in first soft-capped cast-steel APC",
        "6 to 13.5-in light improved cast-steel APC",
        "13.5/14/15/18-in forged-steel APC",
        "12-in Mk 7A Green Boy APC",
        "13.5/14/15-in Mk 5A Green Boy APC",
        "15-in Mk 5A blue-band APC",
        "Average post-WWI CPBC/SAP with Hood",
        "Post-WWI 8-in Mk 1B & 4B SAPC",
        "9.2-in Green Boy coast defense APC",
        "9.2-in Mk 12A coast defense APC",
        "16-in Mk 1B Nelson class APC",
        "14/15/16-in post-1930 APC",
        "15-in Mk 17B Cardonald APC"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Krupp Steel AP Shot/Shell",
        "Average Steel SAP",
        "Average soft-capped APC",
        "Krupp Tough-capped L/3.2 & L/3.4 APC",
        "WWII SAP with Grundring",
        "WWII 38cm/projected 40.6cm SAPC",
        "Post-WWI 15cm L/3.7 APC",
        "Post-WWI 28.3cm L/3.7 APC",
        "WWII 20.3/30.5/15cm L/4.4-4.6 APC",
        "WWII 28.3cm L/4.4 APC",
        "WWII 38cm L/4.4 APC",
        "WWII 40.6cm/projected heavy APC",
        "Krupp long-range lightweight coast artillery APC",
        "Projected 53cm Gerat 36 APC"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Soft-capped Chilled Cast Iron APC",
        "Average Steel AP Shot/Shell",
        "SAPC with soft cap",
        "SAPC with hard cap",
        "Average SAP",
        "33cm APC",
        "38cm APC original French 1940",
        "38cm APC US Crucible Steel AP Mk 1"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Average Steel AP Shot/Shell",
        "Average Soft-capped Steel APC",
        "Average British uncapped CP",
        "Average British soft-capped CPC",
        "British improved cast 6-12-in APC",
        "British improved forged 15-in APC",
        "British 12-in hard-capped Mk 7A APC",
        "British 15-in Mk 5A APC",
        "Italian-design uncapped Common/SAP",
        "Italian-design hard-capped Common/SAPC",
        "Italian-design 15-38cm APC"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Steel AP Shot/Shell",
        "Soft-capped Steel APC",
        "British CP",
        "British CPC",
        "14-in British pre-Jutland APC",
        "36/41cm hard-capped APC",
        "20/36/41cm Mk 6/Type 88 APC with Cap Head",
        "Type 91/1 APC with Cap Head",
        "15.5/20.3cm uncapped Type 91 AP with Cap Head"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Soft-capped Chilled Cast Iron APC Shot",
        "Average Steel AP Shot/Shell",
        "Soft-capped Steel APC Shot/Shell",
        "British-type CP without AP cap",
        "Tough-capped AP Shell/Common",
        "British-type CPC with Tough AP cap",
        "Skoda APC with Tough AP cap"
    },
    new[]
    {
        "Palliser/Gruson Chilled Cast Iron Shot & Common Shell",
        "Soft-capped Chilled Cast Iron APC Shot/Shell",
        "Average Steel AP Shot/Shell",
        "Soft-capped Steel APC Shot/Shell",
        "Post-1906 AP",
        "Post-1906 Tough-capped APC",
        "Post-1906 Common",
        "Post-1906 Tough-capped Common"
    }
};

try
{
    Console.WriteLine("NATHAN OKUN FACE HARDENED ARMOR PENETRATION PROGRAM");
    Console.WriteLine("VERSION 6.9 LEGACY CLI");
    Console.WriteLine();
    Console.WriteLine("Press RETURN/ENTER without entry to use the shown default value.");
    Console.WriteLine();

    var input = await ReadInteractive();
    var showSecondPage = await AskYesNo("Display calculated holing/navy/effective ballistic limits", true);

    while (true)
    {
        var state = Facehard69Legacy.RunSlice(input, new Facehard69LegacyRunOptions { resolveArmorInfo = false });
        PrintInteractiveReport(state, showSecondPage);

        if (!await AskYesNo("Run another striking condition with the same plate and projectile", false))
            break;

        input.OB = await AskNumber("Impact obliquity OB, degrees", input.OB, 0, 80);
        input.VS = await AskNumber("Striking velocity VS, ft/sec", input.VS ?? 1800, 0);
        if (input.WT > input.WB && await AskYesNo("Change nose-covering/cap-head removal state for this run", false))
            await ReadNoseCoveringState(input);
        showSecondPage = await AskYesNo("Display calculated holing/navy/effective ballistic limits", showSecondPage);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

async Task<Facehard69LegacyInput> ReadInteractive()
{
    ShowMenuPage("SELECT FACE-HARDENED ARMOR PLATE TYPE:", armorNames);
    var armor = await AskNumber("Select face-hardened armor type", 16, 1, armorNames.Length);
    var ta = await AskNumber("Plate thickness TA, inches", 10, 0.01);
    var armorValues = ResolveArmorDefaults(armor, ta);

    if (await AskYesNo("Do you want to modify armor plate's parameters", false))
    {
        Console.WriteLine();
        Console.WriteLine("CURRENT ARMOR VALUES:");
        PrintArmorValues(armorValues);
        if (await AskYesNo("Restore all original armor values", false))
            armorValues = ResolveArmorDefaults(armor, ta);
        if (await AskYesNo("Change any armor values", true))
        {
            Console.WriteLine();
            Console.WriteLine("EFFECT OF 'UB' VALUE ONLY CHANGES WHEN 'UB' > 30, 52, 62, 67.5, 75, OR 90.");
            Console.WriteLine("MINIMUM 'Q' & 'QDAM' IS 0.1 AND MAXIMUM 'QDAM' IS 'Q'.");
            armorValues.UB = Math.Truncate(await AskNumber("UB", armorValues.UB, 1, 100));
            armorValues.Q = await AskNumber("Q", armorValues.Q, 0.1);
            armorValues.QDAM = await AskNumber("QDAM", Math.Min(armorValues.QDAM, armorValues.Q), 0.1, armorValues.Q);
            armorValues.CARTWL = await AskOneOf("CARTWL (0, 1, or 2)", armorValues.CARTWL, new[] { 0d, 1d, 2d });
            armorValues.CMPND = await AskOneOf("CMPND (0 or 1)", armorValues.CMPND, new[] { 0d, 1d });
            armorValues.THNCHL = await AskOneOf("THNCHL (0 or 1)", armorValues.UB > 75 ? 1 : armorValues.THNCHL, new[] { 0d, 1d });
            armorValues.SOFTSHAT = await AskOneOf("SOFTSHAT (0, 1, or 2)", armorValues.SOFTSHAT, new[] { 0d, 1d, 2d });
            armorValues.THKTHN = await AskOneOf("THKTHN (0, 1, or 2)", armorValues.THKTHN, new[] { 0d, 1d, 2d });
        }
    }

    var curv = await AskYesNo("Curved plate", false) ? 1 : 0;
    var wd = await AskNumber("Wood backing thickness, inches", 0, 0);
    var cmt = await AskNumber("Cement backing thickness, inches", 0, 0);
    var mtlback = await AskNumber("Metal backing total thickness, inches", 0, 0);
    var nbk = mtlback > 0 ? await AskNumber("Number of metal backing plates", 1, 1) : 0;
    var btp = mtlback > 0 ? await AskNumber("Metal backing type (1-5)", 1, 1, 5) : 0;

    ShowMenuPage("SELECT PROJECTILE'S NATION (EACH NATION HAS ITS OWN PROJECTILE TABLE):", nationNames);
    Console.WriteLine("NOTES:");
    Console.WriteLine("(1) Many older guns kept old ammunition after the listed introduction dates.");
    Console.WriteLine("(2) U.S. Navy ammunition can vary by Mark/Mod, so U.S. has more table entries.");
    Console.WriteLine("(3) Projectile types marked as estimates in the original tables are still estimates here.");
    Console.WriteLine();
    var natn = await AskNumber("Select projectile nation", 2, 1, nationNames.Length);

    var projectileMenu = projectileMenus[(int)natn - 1];
    ShowMenuPage($"SELECT {nationNames[(int)natn - 1].ToUpperInvariant()} PROJECTILE TYPE:", projectileMenu);
    var defaultProjectile = Math.Min(17, projectileMenu.Length);
    var prjtl = await AskNumber("Select projectile type number", defaultProjectile, 1, projectileMenu.Length);
    var caphd = natn == 6 && prjtl >= 8 ? (prjtl == 10 ? 1 : 2) : 0;
    if (caphd == 1)
        Console.WriteLine("'CAP HEAD' is not part of body because it shatters before nose does.");
    else if (caphd == 2)
        Console.WriteLine("Japanese Type 88/91/1 capped projectile: cap-head logic is active.");

    var d = await AskNumber("Projectile diameter/caliber D, inches", 14, 0.01);
    var wt = await AskNumber("Original projectile weight WT, pounds", 1500, 0.01);
    var wb = await AskNumber("Projectile body weight WB after all nose coverings removed, pounds", Math.Min(wt, 1450), wt / 2, wt);
    var ob = await AskNumber("Impact obliquity OB, degrees", 45, 0, 80);
    var vs = await AskNumber("Striking velocity VS, ft/sec", 1800, 0);

    var input = new Facehard69LegacyInput
    {
        ARMOR = armor,
        Q = armorValues.Q,
        QDAM = armorValues.QDAM,
        UB = armorValues.UB,
        CARTWL = armorValues.CARTWL,
        CMPND = armorValues.CMPND,
        SOFTSHAT = armorValues.SOFTSHAT,
        THNCHL = armorValues.THNCHL,
        THKTHN = armorValues.THKTHN,
        TA = ta,
        TEFF = ta,
        D = d,
        WT = wt,
        WB = wb,
        WTSAVE = wt,
        OB = ob,
        VS = vs,
        NATN = natn,
        PRJTL = prjtl,
        CAPHD = caphd,
        CURV = curv,
        WD = wd,
        CMT = cmt,
        MTLBACK = mtlback,
        NBK = nbk,
        BTP = btp
    };

    await ReadNoseCoveringState(input);
    return input;
}

async Task ReadNoseCoveringState(Facehard69LegacyInput input)
{
    input.noseCoveringState = "intact";
    input.WWT = 0;
    input.WCHWT = 0;
    if (input.WT <= input.WB)
        return;

    if ((input.CAPHD ?? 0) > 0)
    {
        Console.WriteLine("If windscreen is removed, Japanese Type 88/91/1 AP/APC cap head is also removed.");
        var capHeadRemoved = await AskYesNo("Have the Windscreen and Cap Head been removed", false);
        if (!capHeadRemoved)
            return;
        input.noseCoveringState = input.CAPHD == 1 ? "all-removed" : "caphead-removed";
        if (input.CAPHD == 2)
            input.WCHWT = await AskNumber("Combined Windscreen and Cap Head weights WCH, pounds", 0, 0, input.WT - input.WB);
    }
    else
    {
        var allRemoved = await AskYesNo("Have all nose coverings/AP cap/hood been removed before impact", false);
        if (allRemoved)
        {
            input.noseCoveringState = "all-removed";
            return;
        }

        var windscreenRemoved = await AskYesNo("Has only the windscreen, if any, been removed", false);
        if (windscreenRemoved)
        {
            input.noseCoveringState = "windscreen-removed";
            input.WWT = await AskNumber("Windscreen weight WWT, pounds", 0, 0, input.WT - input.WB);
            input.WCHWT = input.WWT;
        }
    }
}

ArmorValues ResolveArmorDefaults(double armor, double ta)
{
    var state = Facehard69Legacy.CreateState(new Facehard69LegacyInput
    {
        ARMOR = armor,
        Q = 1,
        QDAM = 1,
        UB = 65,
        CARTWL = 0,
        CMPND = 0,
        SOFTSHAT = 0,
        THNCHL = 0,
        THKTHN = 0,
        TA = ta,
        TEFF = ta,
        D = 14,
        WT = 1500,
        WB = 1500,
        OB = 0
    });
    Facehard69Legacy.FaceCalc(state);
    return new ArmorValues(state.Q, state.QDAM, state.UB, state.CARTWL, state.CMPND, state.SOFTSHAT, state.THNCHL, state.THKTHN);
}

void PrintInteractiveReport(Facehard69LegacyState state, bool showSecondPage)
{
    Console.WriteLine();
    Console.WriteLine("**************** FACEHARD69 LEGACY RESULT ****************");
    WriteLines(state.REPORT);
    if (showSecondPage)
    {
        Console.WriteLine();
        Console.WriteLine("**************** BALLISTIC LIMIT DETAILS ****************");
        WriteLines(state.SECOND_PAGE_REPORT);
    }
    Console.WriteLine();
    Console.WriteLine("**************** PROCESS EXPLANATION ****************");
    WriteLines(state.PROCESS_REPORT);
    Console.WriteLine();
}

void ShowMenuPage(string title, IReadOnlyList<string> items)
{
    Console.WriteLine();
    Console.WriteLine(title);
    Console.WriteLine();
    for (var i = 0; i < items.Count; i++)
        Console.WriteLine($"{(i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(2)}. {items[i]}");
    Console.WriteLine();
}

void PrintArmorValues(ArmorValues values)
{
    Console.WriteLine(Invariant($"UB       = {values.UB}"));
    Console.WriteLine(Invariant($"Q        = {values.Q}"));
    Console.WriteLine(Invariant($"QDAM     = {values.QDAM}"));
    Console.WriteLine(Invariant($"CARTWL   = {values.CARTWL}"));
    Console.WriteLine(Invariant($"CMPND    = {values.CMPND}"));
    Console.WriteLine(Invariant($"THNCHL   = {values.THNCHL}"));
    Console.WriteLine(Invariant($"SOFTSHAT = {values.SOFTSHAT}"));
    Console.WriteLine(Invariant($"THKTHN   = {values.THKTHN}"));
}

async Task<double> AskOneOf(string prompt, double fallback, IReadOnlyCollection<double> allowed)
{
    while (true)
    {
        var value = await AskNumber(prompt, fallback);
        if (allowed.Contains(value))
            return value;
        Console.WriteLine($"Enter one of: {string.Join(", ", allowed.Select(value => value.ToString(CultureInfo.InvariantCulture)))}.");
    }
}

async Task<bool> AskYesNo(string prompt, bool fallback)
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

async Task<string> Ask(string prompt, string fallback)
{
    Console.Write($"{prompt} [{fallback}]: ");
    var answer = (await Console.In.ReadLineAsync())?.Trim();
    return string.IsNullOrEmpty(answer) ? fallback : answer;
}

async Task<double> AskNumber(string prompt, double fallback, double? min = null, double? max = null)
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

void WriteLines(IEnumerable<string> lines)
{
    foreach (var line in lines)
        Console.WriteLine(line);
}

string Invariant(FormattableString value)
{
    return FormattableString.Invariant(value);
}

sealed class ArmorValues
{
    public ArmorValues(double q, double qdam, double ub, double cartwl, double cmpnd, double softshat, double thnchl, double thkthn)
    {
        Q = q;
        QDAM = qdam;
        UB = ub;
        CARTWL = cartwl;
        CMPND = cmpnd;
        SOFTSHAT = softshat;
        THNCHL = thnchl;
        THKTHN = thkthn;
    }

    public double Q;
    public double QDAM;
    public double UB;
    public double CARTWL;
    public double CMPND;
    public double SOFTSHAT;
    public double THNCHL;
    public double THKTHN;
}
