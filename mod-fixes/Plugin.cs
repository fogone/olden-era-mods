using System;
using System.Collections.Generic;
using BepInEx;
using Nobirds.OldenEra.Modding.Common;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Fixes;

// Fixes of the game's own bugs, each a switch of its own (fogone 06.11: a separate mod for the game's bugs). A fix does
// what the game does in the place where it works, with the game's own functions.
[BepInPlugin("nobirds.oldenera.fixes", "Olden Era Fixes", "0.1.1")]
public class Plugin : ModBase
{
    Switch dwellingGrowth;

    // After a map is opened (a new game or a loaded one), the fix looks a few times, while the game is still setting
    // the map up: every two seconds for ten seconds.
    const int CheckEvery = 120, Checks = 5;
    int checksLeft, nextCheck;

    protected override void OnLoad()
    {
        dwellingGrowth = Menu.Toggle("DwellingGrowth", true);
        On(ModEvents.MapLoaded, _ => { checksLeft = Checks; nextCheck = Time.frameCount; });
        OnTick(Tick);
    }

    void Tick()
    {
        if (checksLeft <= 0 || Time.frameCount < nextCheck)
            return;
        checksLeft--;
        nextCheck = Time.frameCount + CheckEvery;
        if (dwellingGrowth.On)
            Logs.Guard("dwelling growth", GiveDwellingBonuses);
    }

    // Issue #7: a unit dwelling on the map gives its owner's towns more weekly growth of its unit ("cityUnitsIncrement
    // FromBarrack", e.g. +2 root iriads from the menhirs). The bonus works while the side's bonus receiver holds it; the
    // game hands it over when the dwelling changes hands (fpq.bnox on 0.82.7: the new side's receiver's Add(bonus,
    // false)). A loaded game makes the dwelling's bonuses anew and gives the dwelling its owner without that step: the
    // growth from every dwelling captured before the save was gone. Here an owned dwelling's bonus with no receiver gets
    // its owner's receiver, by the same call.
    // The developers fixed it in 0.82.08 (hotfix of 4 Nov 2026): drop this fix when the mods move to that version.
    void GiveDwellingBonuses()
    {
        if (Game.MapObjects() is not { } objects)
            return;
        var given = new List<string>();
        foreach (var mapObject in objects)
        {
            if (mapObject?.TryCast<GameDwelling>() is not { } dwelling || dwelling.Bonuses is not { } bonuses)
                continue;
            // Only a dwelling with an owner (the data's owner side, -1 for nobody, and the owner's logic).
            if (mapObject.ObjectData is not { ownerSide: >= 0 } data || mapObject.Owner?.Logic?.BonusReceiver is not { } receiver)
                continue;
            foreach (var bonus in bonuses)
            {
                if (bonus == null || bonus.Receiver != null)
                    continue;
                receiver.Add(bonus, false);
                given.Add($"#{data.idMapObject} (side {data.ownerSide})");
            }
        }
        if (given.Count > 0)
            Logs.Info($"Dwelling growth: gave {given.Count} bonus(es) of owned dwellings to their side: {string.Join(", ", given)}");
    }
}
