using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Nobirds.OldenEra.Modding.Common;
using Hex.Configs;
using Hex.Session.Data;
using Hex.UI.Tooltips;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Visited;

// Map buildings remember who visited them (once, once per hero, once per player, reset daily or weekly), but the
// tooltip often doesn't say so. This plugin adds a row to the map object tooltip: a big status mark (green check =
// visited, amber ring = available) and icons for how often the building can be visited.
// The game's members are its generated names (GameNames.g.cs, made at build time).
[BepInPlugin("nobirds.oldenera.visited", "Olden Era Visited", "0.35.2")]
public partial class Plugin : ModBase
{
    const string RowName = "OE_VisitRow";

    // For the static Harmony patch methods (Outline.cs): they forward to the loaded plugin.
    static Plugin instance;

    ConfigEntry<bool> logObjects;
    Switch showBadges, showCaption, hideGameVisited;
    // While a tooltip is filled with our row: the game's own "already visited" line (our badges say the same) is left
    // out of the texts the game puts into it.
    bool leaveOutVisited;

    protected override string[] ObjectPrefixes => new[] { "OE_Visit" };

    protected override void OnLoad()
    {
        instance = this;
        // The "Моды" tab of the game's settings (mod-settings plugin).
        var menu = Menu;
        showBadges = menu.Toggle("ShowBadges", true);
        showCaption = menu.Toggle("ShowCaption", true);
        hideGameVisited = menu.Toggle("HideGameVisited", true);
        highlight = menu.Choose("Highlight", Highlight.Strobe);
        showGuards = menu.Toggle("ShowGuards", true);
        logObjects = DebugFlag("LogObjects", "Write the visit data of every hovered map object to BepInEx/LogOutput.Logs.");
        WatchTooltips();
        PatchOutlineColors(Harmony);
    }

    // ---------- the game's events (mod-core) ----------

    void WatchTooltips()
    {
        On(ModEvents.SquadTooltipFilling, fill => Logs.Guard("hide", () => OnTooltipFilling(fill)));
        On(ModEvents.TooltipTexts, texts => Logs.Guard("visited line", () => OnTooltipTexts(texts)));
        On(ModEvents.SquadTooltipFilled, fill => Logs.Guard("tooltip", () => OnTooltipFilled(fill)));
        On(ModEvents.DwellingTooltipFilling, _ => HideGuards());
        On(ModEvents.DwellingTooltipFilled, fill => Logs.Guard("guards", () => OnDwellingTooltipFilled(fill)));
    }

    // The plugin's own rows and badges are removed by ModBase.
    protected override void OnUnload()
    {
        HideGuards();
        if (instance == this)
            instance = null;
    }

    // The visit of the object being filled (Describe), worked out before the game fills: to leave its line out then.
    VisitState fillVisit;

    void OnTooltipFilling(SquadTooltipFill fill)
    {
        HideRow(fill.Tooltip);
        HideGuards();
        var mapObject = fill.MapObject?.Typed;
        var data = mapObject?.ObjectData;
        fillVisit = data != null ? Describe(mapObject, data) : null;
        leaveOutVisited = fillVisit?.Visited != null && showBadges.On && hideGameVisited.On;
    }

    // The texts the game puts into the tooltip: while our row shows the visit, its "already visited" line (a text of its
    // own, an empty one when not visited) is left empty, as the game itself does when not visited.
    void OnTooltipTexts(TooltipTexts texts)
    {
        if (!leaveOutVisited)
            return;
        var visited = Game.Translate(Game.TooltipVisitedKey)?.Trim();
        bool IsVisited(string s) => s != null && (s.Trim() == Game.TooltipVisitedKey || (!string.IsNullOrEmpty(visited) && s.Trim() == visited));
        if (!IsVisited(texts.Header) && !IsVisited(texts.Content) && !IsVisited(texts.SubHeader) && !IsVisited(texts.Extra))
        {
            if (!string.IsNullOrEmpty(visited) && new[] { texts.Header, texts.Content, texts.SubHeader, texts.Extra }.Any(s => s != null && s.Contains(visited)))
                Logs.Once($"The game's visited line inside a longer text, left: '{texts.Content}' / '{texts.Extra}'");
            return;
        }
        string Without(string s) => IsVisited(s) ? "" : s;
        texts.Header = Without(texts.Header);
        texts.Content = Without(texts.Content);
        texts.SubHeader = Without(texts.SubHeader);
        texts.Extra = Without(texts.Extra);
    }

    void OnTooltipFilled(SquadTooltipFill fill)
    {
        leaveOutVisited = false;
        if (logObjects.Value)
            Logs.Once($"FILL {fill.Kind} target={(fill.Target != null ? Game.ClassName(fill.Target.Pointer) : "null")}");
        if (fill.MapObject?.Typed is not { } mapObject || mapObject.ObjectData is not { } data)
            return;
        if (logObjects.Value)
            Logs.Once($"GUARD {Game.ClassName(data.Pointer)} sid={data.sidConfig} owner={data.ownerSide} released={data.released} " +
                      $"garrison={data.garnisonParty?.units?.Count.ToString() ?? "none"} rewards={data.rewardSet?.rewards?.Count.ToString() ?? "none"} " +
                      $"game visited={(Game.SelectedHero?.SideOwner is { } owner ? mapObject.VisitedBy(owner).ToString() : "?")}");
        ShowGuards(fill.Tooltip, mapObject, data);
        if (fillVisit is { } visit && showBadges.On)
            ShowRow(fill.Tooltip, visit);
    }
    // ---------- visit status ----------

    // The visit rule of a map object and whether the selected hero (or their player) already used it (Game.VisitFor, in
    // the shared code: the map badges mod asks the same); null when the object has no visit rule.
    VisitState Describe(GameMapObject mapObject, DataObject data)
    {
        var hero = Game.SelectedHero;
        if (logObjects.Value)
        {
            var bank = data.TryCast<ObjEventBank>();
            var config = data.Configs.Select(c => c.TryCast<ObjEventBankConfig>()).FirstOrDefault(c => c != null);
            var visitors = bank?.visitorsList != null ? string.Join(",", Scene.Items(bank.visitorsList)) : "-";
            var side = hero?.HeroSide;
            Logs.Once($"OBJECT {Game.ClassName(data.Pointer)} sid={data.sidConfig} id={data.idMapObject} owner={data.ownerSide} " +
                      $"visitType={config?.visitType} reset={config?.visitorsResetType} tooltipVisit={data.Configs.FirstOrDefault()?.tooltipVisitType} " +
                      $"visitors=[{visitors}] scoutings=[{(data.sideScoutings != null ? string.Join(",", Scene.Items(data.sideScoutings)) : "-")}] " +
                      $"hero visit id={hero?.VisitId} side index={side?.index} lastSelectedHero={side?.lastSelectedHero}");
        }
        return mapObject.VisitFor(data, hero);
    }

    // The visit in words: visited or available (in its colour), the rule, the period.
    static IEnumerable<string> Caption(VisitState visit)
    {
        if (visit.Visited is { } visited)
            yield return visited ? $"<color=#{ColorUtility.ToHtmlStringRGB(Badges.Green)}>{Texts.Get("oe.visited.text.Visited")}</color>"
                                 : $"<color=#{ColorUtility.ToHtmlStringRGB(Badges.Amber)}>{Texts.Get("oe.visited.text.Available")}</color>";
        yield return Texts.Get(visit.Rule switch
        {
            VisitRule.Once => "oe.visited.text.OneTime",
            VisitRule.PerHero => "oe.visited.text.PerHero",
            VisitRule.PerSide => "oe.visited.text.PerPlayer",
            _ => "oe.visited.text.Unlimited",
        });
        if (visit.Reset == VisitReset.Week)
            yield return Texts.Get("oe.visited.text.Weekly");
        else if (visit.Reset == VisitReset.Day)
            yield return Texts.Get("oe.visited.text.Daily");
    }


    // ---------- UI ----------

    const string BadgesName = "OE_VisitBadges";
    const int BadgeSlots = 3;
    const float BadgeGap = 6; // between the badges and the tooltip's left border

    // The caption goes on top of the tooltip text as a wrapped line; the badges stand outside the tooltip, left of its border.
    void ShowRow(BhTooltipSquad tooltip, VisitState visit)
    {
        var layout = tooltip.layout;
        if (layout == null)
            return;
        var sample = tooltip.header ?? tooltip.container.behaviorSquad;
        var size = sample != null ? sample.fontSize : 24f;

        var row = layout.transform.Find(RowName) ?? CreateCaption(layout.transform, sample, size);
        row.GetComponent<TextMeshProUGUI>().text = string.Join(" · ", Caption(visit));
        row.gameObject.SetActive(showCaption.On);
        // Top of the text block, right after its "Center" background so it isn't drawn under it.
        var background = layout.transform.Find("Center");
        row.SetSiblingIndex(background != null && background != row ? background.GetSiblingIndex() + 1 : 0);

        // On the tooltip root (its Image is the panel with the frame), so the badges follow the panel.
        var badges = tooltip.transform.Find(BadgesName) ?? CreateBadges(tooltip.transform, size);
        var keys = BadgeIcons.Of(visit);
        for (var i = 0; i < BadgeSlots; i++)
        {
            var slot = badges.Find($"Badge{i}").GetComponent<Image>();
            slot.gameObject.SetActive(i < keys.Count);
            if (i < keys.Count)
                slot.sprite = BadgeIcons.Icon(keys[i]);
        }
        badges.SetAsLastSibling();
        badges.gameObject.SetActive(true);
        LayoutRebuilder.MarkLayoutForRebuild(layout.GetComponent<RectTransform>());
        if (!dumped && logObjects.Value)
        {
            dumped = true;
            Scene.Tree(tooltip.transform, Log.LogMessage, 4);
        }
    }

    void HideRow(BhTooltipSquad tooltip)
    {
        if (tooltip.layout == null)
            return;
        var badges = tooltip.transform.Find(BadgesName);
        if (badges != null)
            badges.gameObject.SetActive(false);
        var row = tooltip.layout.transform.Find(RowName);
        if (row != null)
            row.gameObject.SetActive(false);
    }

    // The caption wraps inside the tooltip instead of widening it: zero preferred width, flexible width.
    static Transform CreateCaption(Transform parent, TMP_Text sample, float size)
    {
        var go = new GameObject(RowName, Il2CppType.Of<RectTransform>());
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (sample != null)
            text.font = sample.font;
        text.fontSize = size * 0.75f;
        text.color = CaptionColor;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        var element = go.AddComponent<LayoutElement>();
        element.preferredWidth = 0;
        element.flexibleWidth = 1;
        return go.transform;
    }

    // A column of equal badges just outside the tooltip's left edge.
    static Transform CreateBadges(Transform parent, float size)
    {
        var go = new GameObject(BadgesName, Il2CppType.Of<RectTransform>());
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        var badge = size * 1.6f;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-BadgeGap, -badge * 0.9f);
        rect.sizeDelta = new Vector2(badge, badge * BadgeSlots + 4 * (BadgeSlots - 1));
        var column = go.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        for (var i = 0; i < BadgeSlots; i++)
        {
            var slot = new GameObject($"Badge{i}", Il2CppType.Of<RectTransform>());
            slot.transform.SetParent(go.transform, false);
            var image = slot.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var element = slot.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = element.minWidth = element.minHeight = badge;
        }
        return go.transform;
    }

    // Debug: the tooltip's object tree once, to see where its frame and background are.
    bool dumped;


    // The caption's colour, the tooltip's.
    static readonly Color CaptionColor = new(0.66f, 0.60f, 0.48f);
}
