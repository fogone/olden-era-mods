using System;
using System.Collections.Generic;
using System.Linq;

namespace OldenEra.Modding.Manager;

// What the Installed tab does to several mods at once (remove, switch on or off, update): in which order, and what is
// left out. No game, no BepInEx (the tests use it).
public static class Batch
{
    // The ids that are installed (once each, in the order given), dependents before what they need: a mod that requires
    // another is handled first. A cycle (it should not exist) is appended as it stands.
    public static List<string> DependentsFirst(IEnumerable<string> ids, IEnumerable<InstalledMod> installed)
    {
        var mods = installed.ToList();
        var left = ids.Where(id => mods.Any(m => Same(m.Id, id))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var order = new List<string>();
        while (left.Count > 0)
        {
            // Not needed by any other mod still waiting here.
            var next = left.FirstOrDefault(id => !left.Any(other => !Same(other, id) && Requires(mods, other, id)));
            if (next == null)
            {
                order.AddRange(left);
                break;
            }
            order.Add(next);
            left.Remove(next);
        }
        return order;
    }

    // The mods to remove, in the order they can go: dependents first; one that a mod staying installed requires is
    // left out (the manager's Remove refuses it too).
    public static List<string> Removable(IEnumerable<string> ids, IEnumerable<InstalledMod> installed)
    {
        var mods = installed.ToList();
        var removed = new List<string>();
        foreach (var id in DependentsFirst(ids, mods))
            if (!mods.Any(m => !removed.Contains(m.Id, StringComparer.OrdinalIgnoreCase) && Requires(mods, m.Id, id)))
                removed.Add(id);
        return removed;
    }

    // The order to switch mods on (what a mod needs first) or off (dependents first).
    public static List<string> SwitchOrder(IEnumerable<string> ids, IEnumerable<InstalledMod> installed, bool on)
    {
        var order = DependentsFirst(ids, installed);
        if (on)
            order.Reverse();
        return order;
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static bool Requires(List<InstalledMod> mods, string id, string required) =>
        mods.FirstOrDefault(m => Same(m.Id, id))?.Requires.Contains(required, StringComparer.OrdinalIgnoreCase) == true;
}
