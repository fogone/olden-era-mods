using System;
using System.Collections.Generic;
using System.Linq;
using Hex.GameHub.UICommon;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// A new faction's pictures in the lobby (#56). The faction itself is data (DB/data.json's lists of factions, its
// DB/fractions entry, its city, heroes and units); what the lobby shows of a faction is not: the game's SoFractions
// assets hold a FractionLobbyAsset per faction id (its icon, the slot's and the statistics' backgrounds, the versus
// screen's, the big icon, the card), and the lobby, the HUD and the pick screens take them by id. A faction added here
// gets an entry made from the faction it is `like` (its pictures), each picture given replacing that one.
//
//   Factions.AddLobby("cove", like: "human", new Dictionary<string, Sprite> { ["icon"] = myIcon });
//
// A data mod needs no code for it: its archive's Lobby/<id>.json ({"like": "human"}) and Lobby/<id>/<picture>.png
// (icon, slotFon, statisticFon, versusFon, bigIcon, card) are added so (ModContent).
public static class Factions
{
    // The pictures of a lobby entry, as the game names them.
    public static readonly IReadOnlyList<string> Pictures = new[] { "icon", "slotFon", "statisticFon", "versusFon", "bigIcon", "card" };

    sealed record Lobby(string Id, string Like, IReadOnlyDictionary<string, Func<Sprite>> Pictures);

    static readonly List<Lobby> lobbies = new();
    static readonly HashSet<string> unlike = new();
    static bool started;
    static int nextCheck;

    // The faction `id` in the lobby with the pictures of `like`, those given (by the names in Pictures) instead.
    public static void AddLobby(string id, string like, IReadOnlyDictionary<string, Sprite> pictures = null) =>
        AddLobby(id, like, pictures?.ToDictionary(p => p.Key, p => (Func<Sprite>)(() => p.Value)));

    internal static void AddLobby(string id, string like, IReadOnlyDictionary<string, Func<Sprite>> pictures)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(like))
            return;
        foreach (var name in pictures?.Keys.Except(Pictures) ?? Enumerable.Empty<string>())
            Game.Log?.Invoke($"Factions: {id}: no lobby picture '{name}' (there are {string.Join(", ", Pictures)})");
        lobbies.RemoveAll(l => l.Id == id);
        lobbies.Add(new Lobby(id, like, pictures ?? new Dictionary<string, Func<Sprite>>()));
        nextCheck = 0;
        if (!started)
        {
            started = true;
            Ticker.Add(typeof(Factions), Fill);
        }
    }

    // The factions added to the lobby.
    public static IEnumerable<string> InLobby => lobbies.Select(l => l.Id);

    // Every 60 frames: our entries into each SoFractions that misses them (the assets load with the menus).
    static void Fill()
    {
        if (Time.frameCount < nextCheck)
            return;
        nextCheck = Time.frameCount + 60;
        foreach (var assets in Resources.FindObjectsOfTypeAll<SoFractions>())
        {
            var byId = assets.dict_;
            if (byId == null || byId.Count == 0)
                continue;
            var entries = assets.fractions?.ToList() ?? new List<FractionLobbyAsset>();
            var changed = false;
            foreach (var lobby in lobbies)
            {
                if (byId.ContainsKey(lobby.Id))
                    continue;
                if (!byId.TryGetValue(lobby.Like, out var like) || like == null)
                {
                    if (unlike.Add(lobby.Id))
                        Game.Log?.Invoke($"Factions: {lobby.Id}: no faction '{lobby.Like}' in the lobby to be like");
                    continue;
                }
                var entry = new FractionLobbyAsset
                {
                    sid = lobby.Id,
                    icon = Picture(lobby, "icon") ?? like.icon,
                    slotFon = Picture(lobby, "slotFon") ?? like.slotFon,
                    statisticFon = Picture(lobby, "statisticFon") ?? like.statisticFon,
                    versusFon = Picture(lobby, "versusFon") ?? like.versusFon,
                    bigIcon = Picture(lobby, "bigIcon") ?? like.bigIcon,
                    card = Picture(lobby, "card") ?? like.card,
                };
                byId[lobby.Id] = entry;
                entries.Add(entry);
                changed = true;
                Game.Log?.Invoke($"Factions: {lobby.Id} (like {lobby.Like}) in the lobby assets {assets.name}");
            }
            if (changed)
                assets.fractions = new Il2CppReferenceArray<FractionLobbyAsset>(entries.ToArray());
        }
    }

    static Sprite Picture(Lobby lobby, string name)
    {
        if (!lobby.Pictures.TryGetValue(name, out var make))
            return null;
        try { return make(); }
        catch (Exception e)
        {
            Game.Log?.Invoke($"Factions: {lobby.Id} {name}: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }
}
