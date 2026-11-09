using System;
using System.Collections.Generic;
using UnityEngine;
using static Nobirds.OldenEra.Modding.Common.Badges;   // the glyphs' shapes: Segment, Circle, RoundBox, Min

namespace Nobirds.OldenEra.Modding.Common;

// Our drawn badges: the visit status, the visit period, the guard, the pickups and mines. The tooltip of a building
// shows the visit ones ("Посещения"), the map the others too ("Значки на карте").
public enum Badge { Visited, Available, Once, PerHero, PerSide, Unlimited, Week, Day, Guard, Res, OwnRes, Item, Chest }

// The badges as sprites (Badges: dark plate, rounded frame, glyph), drawn once per mod. The status ones in their colour
// (visited green, available amber, guard red, pickups amber), the period ones gold in a plain frame.
public static class BadgeIcons
{
    static readonly Dictionary<Badge, Sprite> icons = new();

    // The status badge of a visit (none when the building has no limit), then its rule and period.
    public static List<Badge> Of(VisitState visit)
    {
        var list = new List<Badge>();
        if (visit.Visited is { } visited)
            list.Add(visited ? Badge.Visited : Badge.Available);
        list.AddRange(PeriodOf(visit));
        return list;
    }

    // The rule and period badges of a visit: once, per hero, per player or unlimited; weekly or daily.
    public static IEnumerable<Badge> PeriodOf(VisitState visit)
    {
        yield return visit.Rule switch
        {
            VisitRule.Once => Badge.Once,
            VisitRule.PerHero => Badge.PerHero,
            VisitRule.PerSide => Badge.PerSide,
            _ => Badge.Unlimited,
        };
        if (visit.Reset == VisitReset.Week)
            yield return Badge.Week;
        else if (visit.Reset == VisitReset.Day)
            yield return Badge.Day;
    }

    static Color? Tone(Badge key) => key switch
    {
        Badge.Visited or Badge.OwnRes => Green,
        Badge.Guard => Red,
        Badge.Available or Badge.Res or Badge.Item or Badge.Chest => Amber,
        _ => null,
    };

    public static Sprite Icon(Badge key)
    {
        if (icons.TryGetValue(key, out var sprite) && sprite != null)
            return sprite;
        var tone = Tone(key);
        return icons[key] = Draw(tone ?? Gold, (x, y) => Glyph(key, x, y), frameWidth: 1.5f, strokeWidth: tone != null ? 4f : 2.5f,
                                 frame: tone ?? Frame);
    }

    // Distance to the strokes of a glyph inside the badge frame.
    static float Glyph(Badge key, float x, float y)
    {
        switch (key)
        {
            case Badge.Visited: // tick
                return Math.Min(Segment(x, y, 18, 33, 28, 22), Segment(x, y, 28, 22, 46, 43));
            case Badge.Available: // exclamation mark
                return Math.Min(Segment(x, y, 32, 27, 32, 50), Circle(x, y, 32, 15, 1));
            case Badge.Once: // the digit 1
                return Min(Segment(x, y, 32, 15, 32, 49), Segment(x, y, 32, 49, 25, 43), Segment(x, y, 25, 15, 39, 15));
            case Badge.PerHero: // head and shoulders
                var shoulders = y >= 15 ? Math.Abs(Circle(x, y, 32, 15, 14)) : Segment(x, y, 18, 15, 46, 15);
                return Math.Min(Math.Abs(Circle(x, y, 32, 41, 7)), shoulders);
            case Badge.PerSide: // flag on a pole
                return Min(Segment(x, y, 22, 13, 22, 51), Segment(x, y, 22, 50, 45, 43), Segment(x, y, 45, 43, 22, 36));
            case Badge.Unlimited: // infinity, two loops
                return Math.Min(Math.Abs(Circle(x, y, 23, 32, 8)), Math.Abs(Circle(x, y, 41, 32, 8)));
            case Badge.Week: // calendar page with a 7
                return Math.Min(Min(Math.Abs(RoundBox(x, y, 32, 30, 16, 14, 3)), Segment(x, y, 16, 37, 48, 37), Segment(x, y, 24, 42, 24, 49)),
                                Min(Segment(x, y, 40, 42, 40, 49), Segment(x, y, 26, 32, 38, 32), Segment(x, y, 38, 32, 30, 20)));
            case Badge.Day: // sun
                var d = Math.Abs(Circle(x, y, 32, 32, 7));
                for (var k = 0; k < 8; k++)
                {
                    var a = k * Mathf.PI / 4;
                    d = Math.Min(d, Segment(x, y, 32 + 12 * Mathf.Cos(a), 32 + 12 * Mathf.Sin(a), 32 + 18 * Mathf.Cos(a), 32 + 18 * Mathf.Sin(a)));
                }
                return d;
            case Badge.Guard: // crossed swords
                return Math.Min(Min(Segment(x, y, 17, 17, 47, 47), Segment(x, y, 47, 17, 17, 47), Segment(x, y, 14, 24, 24, 14)), Segment(x, y, 40, 14, 50, 24));
            case Badge.Res: // gem
            case Badge.OwnRes: // gem, green: a mine of ours
                return Math.Min(Min(Segment(x, y, 21, 45, 43, 45), Segment(x, y, 43, 45, 50, 37), Segment(x, y, 50, 37, 32, 14)),
                                Min(Segment(x, y, 32, 14, 14, 37), Segment(x, y, 14, 37, 21, 45), Segment(x, y, 14, 37, 50, 37)));
            case Badge.Item: // sword
                return Min(Segment(x, y, 25, 25, 47, 47), Segment(x, y, 17, 31, 31, 17), Segment(x, y, 25, 25, 17, 17));
            case Badge.Chest: // chest with a rounded lid and a lock
                return Min(Math.Abs(RoundBox(x, y, 32, 25, 17, 10, 2)), y >= 35 ? Math.Abs(RoundBox(x, y, 32, 35, 17, 11, 8)) : float.MaxValue,
                           Math.Abs(Circle(x, y, 32, 33, 3)));
            default:
                return float.MaxValue;
        }
    }
}
