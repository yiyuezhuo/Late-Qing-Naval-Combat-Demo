using System.Globalization;
using System.Text;
using NavalCombatReplayAnalyzer.Models;

namespace NavalCombatReplayAnalyzer.Services;

public class AcmiExporter
{
    public string Export(ReplayViewModel replay, string language = "english", string shape = "ship.obj")
    {
        shape = string.IsNullOrWhiteSpace(shape) ? "" : shape.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("FileType=text/acmi/tacview");
        sb.AppendLine("FileVersion=2.2");
        sb.AppendLine($"0,ReferenceTime={replay.startTime:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine("0,Title=Naval Combat Replay");

        var objectIds = replay.ships
            .Select((ship, index) => (ship, id: index + 1))
            .ToDictionary(pair => pair.ship.id, pair => pair.id);

        foreach (var time in replay.sampleTimes)
        {
            var seconds = Math.Max(0, (time - replay.startTime).TotalSeconds);
            sb.AppendLine($"#{seconds.ToString("0.###", CultureInfo.InvariantCulture)}");

            foreach (var ship in replay.ships)
            {
                var point = ReplayAnalyzerService.GetPointAtOrBefore(ship.track, time);
                if (point == null)
                    continue;

                var objectId = objectIds[ship.id];
                var shipName = ReplayAnalyzerService.SelectName(ship.nameVariants, language, ship.name);
                if (string.IsNullOrWhiteSpace(shipName))
                    shipName = ship.id;
                var transform = string.Join("|", new[]
                {
                    point.lon.ToString("0.######", CultureInfo.InvariantCulture),
                    point.lat.ToString("0.######", CultureInfo.InvariantCulture),
                    "0",
                    "0",
                    "0",
                    point.headingDeg.ToString("0.###", CultureInfo.InvariantCulture)
                });
                sb.Append(objectId);
                sb.Append(",T=");
                sb.Append(transform);
                sb.Append(",Name=");
                sb.Append(Escape(shipName));
                sb.Append(",Type=Sea+Watercraft+Warship");
                if (!string.IsNullOrWhiteSpace(shape))
                {
                    sb.Append(",Shape=");
                    sb.Append(Escape(shape));
                }
                sb.Append(",Color=");
                sb.Append(ResolveTacviewColor(ship.color));
                sb.Append(",LongName=");
                sb.Append(Escape(shipName));
                sb.AppendLine();
            }
        }

        foreach (var shot in replay.shots)
        {
            var seconds = Math.Max(0, (shot.time - replay.startTime).TotalSeconds);
            var shooterName = ReplayAnalyzerService.SelectName(shot.shooterNameVariants, language, shot.shooterName);
            var targetName = ReplayAnalyzerService.SelectName(shot.targetNameVariants, language, shot.targetName);
            sb.AppendLine($"#{seconds.ToString("0.###", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"0,Event=Message|{Escape($"{shot.weapon}: {shooterName} -> {targetName}, DP {shot.damagePoint:0.##}")}");
        }

        return sb.ToString();
    }

    static string Escape(string value)
    {
        return (value ?? "")
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("\n", " ");
    }

    static string ResolveTacviewColor(string color)
    {
        return color?.ToLowerInvariant() switch
        {
            "#d63b2f" => "Red",
            "d63b2f" => "Red",
            "#2f6fd6" => "Blue",
            "2f6fd6" => "Blue",
            _ => "Blue"
        };
    }
}
