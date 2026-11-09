using System;
using System.Collections.Generic;
using System.Linq;
using Hex.Session.Battle;
using Nobirds.OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.BattleCounter;

// By a choice (fogone 06.11), one more row in the forecast: the game's health icon and the defender's health, and when the
// defender answers, the swords and the attacker's health (as in the rows above; the row's icon says it is health for
// both sides, so none before the attacker's). No words: numbers and the game's icons.
// The top creature's and the whole stack's health, or (smart) the one that matters for the hit. A stack's health as the game keeps it: one
// creature's (unit.stats.hp) for every creature under the top one (UnitData.fullStacks) and the top one's own
// (UnitData.TopHealth, what the unit plate's health bar shows).
public partial class Plugin
{
    // Both: the top creature's and the whole stack's health, always. Smart, the same for both sides (fogone 06.11): a lone
    // creature's own; of a stack, its whole health, and the top creature's before it when the hit kills fewer than two
    // creatures (then what is left of the top one matters; when it kills several, it does not).
    public enum HealthShown { Off, Both, Smart }

    Choice<HealthShown> healthShown;
    const string HealthRowName = "OE_CounterHealthRow";
    Sprite heartSprite;

    // The units of a unit attack's forecast, by forecast.
    readonly Dictionary<IntPtr, (Unit attacker, Unit defender)> fights = new();

    // The health shown for a unit: numbers only, no slash; "350 · 12350" is the top creature's, then the stack's.
    static string Health(Unit unit, HealthShown shown, int killedMax)
    {
        if (unit?.Data is not { } data || unit.stats is not { } stats)
            return "";
        var top = data.TopHealth;
        if (data.fullStacks == 0)
            return $"{top}";   // a lone creature: its own is the stack's
        var all = data.fullStacks * stats.hp + top;
        return shown == HealthShown.Both || killedMax < 2 ? $"{top} · {all}" : $"{all}";
    }
    // The row for the forecast the panel shows: under each step, the health of the one it hits as it is then (the
    // defender's under a strike, the attacker's under a counter); before the game's decision (null), the defender's.
    // A health still whole shows as chosen; one a step before took from, as the range of the whole stack's health it may
    // have then. Only by its own choice (fogone 06.11).
    void ShowHealth(BhHealDamageForecastPanel panel, GameForecast forecast, List<Step> steps)
    {
        var mode = healthShown.Value(HealthShown.Off);
        var shown = mode != HealthShown.Off && forecast != null && fights.ContainsKey(forecast.Pointer);
        var row = HealthRow(panel, shown);
        if (row == null)
            return;
        row.gameObject.SetActive(shown);
        if (shown)
        {
            var (attacker, defender) = fights[forecast.Pointer];
            var separator = Separator(row);
            string Of(Step step) => !step.TargetUntouched ? step.TargetHealth.Text(separator)
                : Health(step.ByDefender ? attacker : defender, mode, step.Numbers.KilledMax);
            SetRowNumber(row, steps is { Count: > 0 } ? Of(steps[0]) : Health(defender, mode, Numbers.Of(forecast).KilledMax));
            for (var i = 1; i <= MaxColumns; i++)
            {
                var step = steps != null && i < steps.Count ? steps[i] : null;
                RowColumn(row, i, step != null ? Of(step) : null, step?.ByDefender == true, white: true);
            }
            Canvas.ForceUpdateCanvases();
        }
        PlaceHealthRow(panel, row, shown);
    }

    // The game's icon of health, by its sprite's name: the one of the unit's stats (Icon_Stats_Hp), else a heart; the
    // candidates are written to the log once (06.11: heart_icon, Property 1=Icon_Stats_Hp, unit_health, ...).
    Sprite HeartSprite()
    {
        if (heartSprite != null)
            return heartSprite;
        var names = Game.SpriteNames(n => n.IndexOf("heart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          n.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          n.StartsWith("Icon_Hp", StringComparison.OrdinalIgnoreCase) || n.EndsWith("_HP", StringComparison.OrdinalIgnoreCase));
        heartSprite = Game.SpriteMatching(n => n.EndsWith("Icon_Stats_Hp", StringComparison.OrdinalIgnoreCase))
                      ?? Game.SpriteNamed("unit_health") ?? Game.SpriteNamed("heart_icon");
        Logs.Once($"Health icon: {(heartSprite != null ? heartSprite.name : "none")}; candidates: {string.Join(", ", names)}");
        return heartSprite;
    }

    // The panel places its rows by hand, not by a layout (the kills row above the damage row; 0.11 of this mod found it so
    // and made its rows the same way): ours goes under the damage row by the same step as the damage row under the kills
    // row, and the panel's background (the damage row's ShadowPanel) is stretched down over it, with a margin as its
    // lower edge fades out; back to its size without our row, and when the mod unloads (the size lives in the game's
    // object: a reloaded mod would take a stretched one for the game's). Not wider: the background goes with the damage
    // row's width, and the table makes the rows equally wide (stretching it after a swinging row's width, 06.11, made
    // it miles wide).
    const float ShadowMargin = 0.8f;   // more background below our row, in row heights (0.11)
    readonly Dictionary<IntPtr, (RectTransform shadow, Vector2 size, Vector2 position)> shadows = new();

    void RestoreShadows()
    {
        foreach (var (shadow, size, position) in shadows.Values)
            if (shadow != null)
            {
                shadow.sizeDelta = size;
                shadow.anchoredPosition = position;
            }
        shadows.Clear();
    }

    void PlaceHealthRow(BhHealDamageForecastPanel panel, Transform row, bool shown)
    {
        var damage = panel.hpPanel.GetComponent<RectTransform>();
        var height = damage.rect.height;
        // The game's own step between its rows; one row height when its kills row is not there.
        var step = panel.stacksPanel != null && panel.stacksPanel.GetComponent<RectTransform>() is { } kills && kills.anchoredPosition.y > damage.anchoredPosition.y
            ? kills.anchoredPosition.y - damage.anchoredPosition.y
            : height;
        var rowRect = row.GetComponent<RectTransform>();
        if (shown)
            rowRect.anchoredPosition = damage.anchoredPosition - new Vector2(0f, step);
        if (damage.transform.Find("ShadowPanel")?.GetComponent<RectTransform>() is not { } shadow)
            return;
        if (!shadows.TryGetValue(shadow.Pointer, out var original))
            shadows[shadow.Pointer] = original = (shadow, shadow.sizeDelta, shadow.anchoredPosition);
        var down = shown ? step + height * ShadowMargin : 0f;
        shadow.sizeDelta = original.size + new Vector2(0f, down);
        // Its top edge stays where it is: it grows down.
        shadow.anchoredPosition = original.position - new Vector2(0f, down * (1f - shadow.pivot.y));
    }

    // The background again for the health row as it is now (after the swords were aligned, the rows may be wider).
    void RefreshHealthBackground(BhHealDamageForecastPanel panel)
    {
        if (panel.hpPanel != null && panel.hpPanel.transform.parent.Find(HealthRowName) is { } row && row.gameObject.activeSelf)
            PlaceHealthRow(panel, row, true);
    }

    // Our copy of the panel's damage row under it, made once: the row's icon (the heart, when the game has it), its gap
    // and first number stay; its other numbers, its background and any of our items copied with it are off.
    Transform HealthRow(BhHealDamageForecastPanel panel, bool needed)
    {
        var source = panel.hpPanel != null ? panel.hpPanel.transform : null;
        if (source == null)
            return null;
        var row = source.parent.Find(HealthRowName);
        if (row != null || !needed)
            return row;
        row = UnityEngine.Object.Instantiate(source.gameObject, source.parent, false).transform;
        row.name = HealthRowName;
        row.SetSiblingIndex(source.GetSiblingIndex() + 1);
        var heart = HeartSprite();
        var first = true;
        for (var i = 0; i < row.childCount; i++)
        {
            var child = row.GetChild(i);
            if (child.name.StartsWith("OE_"))
                UnityEngine.Object.Destroy(child.gameObject);
            else if (child.GetComponent<TMP_Text>() != null && first)
                first = false;
            else if (heart != null && child.name is "IconShadow" or "Space")
            {
                if (child.name == "IconShadow")
                    foreach (var image in child.GetComponentsInChildren<Image>(true))
                        image.sprite = heart;
            }
            else
                child.gameObject.SetActive(false);
        }
        return row;
    }

    // The row's first number item (a shadow text with the visible "Title" under it) shows the text.
    static void SetRowNumber(Transform row, string text)
    {
        var item = Enumerable.Range(0, row.childCount).Select(row.GetChild)
            .FirstOrDefault(c => !c.name.StartsWith("OE_") && c.GetComponent<TMP_Text>() != null);
        if (item == null)
            return;
        item.gameObject.SetActive(true);
        foreach (var t in item.GetComponentsInChildren<TMP_Text>(true))
        {
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            // White: health, not damage (the game's damage row is tinted).
            t.text = t.name == "Title" ? $"<color=#FFFFFF>{text}</color>" : text;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(row.GetComponent<RectTransform>());
    }
}
