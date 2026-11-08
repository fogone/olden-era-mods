using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Hex.Configs;
using Hex.Session.Battle;
using OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.Modding.BattleCounter;

// The battle's damage forecast as the attack's sequence, left to right (fogone 06.11, issue #8): the defender's counter
// before the strike, the strike, the counter after it, Double Strike's second strike (SecondStrike.cs: what happens and
// each step's numbers). The first step goes into the game's own numbers (its forecast, shown again by the game's
// panel); every next one is a column of ours after them: the crossed swords and amber numbers for the defender's
// counter, the row's own icon and white numbers for the attacker's strike. Whether the defender answers is the game's
// own decision (ModEvents.CounterDecided); the numbers are the game's own forecast for each hit (with the roles swapped
// for a counter).
[BepInPlugin("nobirds.oldenera.battlecounter", "Olden Era Battle Counter", "0.48.2")]
public partial class Plugin : ModBase
{
    Switch showCounter;

    protected override string[] ObjectPrefixes => new[] { "OE_Counter" };

    // The game's objects the mod stretched go back to their own size (its own objects ModBase destroys).
    protected override void OnUnload() => RestoreShadows();

    protected override void OnLoad()
    {
        Game.Log = Logs.Info;
        showCounter = Menu.Toggle("ShowCounter", true);
        secondShown = Menu.Choose("SecondStrike", SecondStrikeShown.Column);
        healthShown = Menu.Choose("HealthShown", HealthShown.Off);
        // The game's events (mod-core): an attack's forecast built, another forecast made, the panel shown, the game's
        // decision whether the defender answers (eqe.bhqj on 0.82.7).
        On(ModEvents.ForecastBuilt, built => Logs.Guard("forecast", () => OnForecastBuilt(built)));
        On(ModEvents.OtherForecast, forecast => Forget(forecast.Pointer));
        On(ModEvents.ForecastShown, shown => Logs.Guard("show", () => OnForecastShown(shown)));
        On(ModEvents.CounterDecided, decided => Logs.Guard("counter decision", () => OnCounterDecided(decided)));
        OnTick(KeepAligned);
    }

    // ---------- the attack ----------

    readonly record struct Range(int Min, int Max)
    {
        public override string ToString() => Text("–");

        // As the game writes a range in its forecast: min, its separator, max (Separator).
        public string Text(string separator) => Min == Max ? $"{Min}" : $"{Min}{separator}{Max}";
    }

    // The numbers of a forecast the panel shows: damage min and max, killed min and max.
    readonly record struct Numbers(int DamageMin, int DamageMax, int KilledMin, int KilledMax)
    {
        public static Numbers Of(GameForecast f) => new(f.DamageMin, f.DamageMax, f.KilledMin, f.KilledMax);

        public void Into(GameForecast f) => (f.DamageMin, f.DamageMax, f.KilledMin, f.KilledMax) = (DamageMin, DamageMax, KilledMin, KilledMax);

        public Range Damage => new(DamageMin, DamageMax);
        public Range Killed => new(KilledMin, KilledMax);
    }

    // By forecast panel: the forecast it shows.
    readonly Dictionary<IntPtr, (BhHealDamageForecastPanel panel, GameForecast forecast)> panels = new();
    bool building;   // the mod's own runs of the game's forecast builder

    // An attack's forecast the game built (an area attack gets one per unit it hits; which of them answers is the game's
    // decision): its units, its parameters and the game's own numbers, for the sequence once the game decides.
    void OnForecastBuilt(ForecastBuilt built)
    {
        if (building)
            return;
        var forecast = built.Forecast.Typed;
        if (forecast == null)
            return;
        Forget(forecast.Pointer);
        if (fights.Count > 256)
        {
            fights.Clear();
            strikes.Clear();
            sequences.Clear();
        }
        var p = built.Params;
        if (p.caster?.TryCast<Unit>() is not { } attacker || p.target?.TryCast<Unit>() is not { } defender)
            return;
        fights[forecast.Pointer] = (attacker, defender);
        strikes[forecast.Pointer] = (p, built.Flag, Numbers.Of(forecast));
    }

    // ---------- the panel ----------

    void OnForecastShown(ForecastShown shown)
    {
        var panel = shown.Panel;
        var forecast = shown.Forecast?.Typed;
        panels[panel.Pointer] = (panel, forecast);
        // The columns wait for the game's decision, made right after this in the same hover.
        ShowSteps(panel, null);
        ShowHealth(panel, forecast, null);
    }

    string lastDecision;

    void OnCounterDecided(CounterDecided decided)
    {
        if (decided.Defender?.TryCast<Unit>() is not { } defender)
            return;
        foreach (var (panel, forecast) in panels.Values.ToList())
        {
            if (panel == null || forecast == null || !fights.TryGetValue(forecast.Pointer, out var fight) || fight.defender.Pointer != defender.Pointer)
                continue;
            var sequence = SequenceOf(decided, forecast, fight, secondShown.Value(SecondStrikeShown.Off));
            if (sequence == null)
                continue;
            // What is shown: the counters by the switch (the strikes still count them), the second strike is in the
            // sequence only by its choice.
            var steps = sequence.Steps.Where(s => !s.ByDefender || showCounter.On).ToList();
            if (steps.Count == 0)
                continue;
            // The first step into the game's numbers, shown again by the game's panel (which resets our columns: set
            // below).
            if (Numbers.Of(forecast) != steps[0].Numbers)
            {
                steps[0].Numbers.Into(forecast);
                panel.Show(forecast);
            }
            ShowSteps(panel, steps);
            ShowHealth(panel, forecast, steps);
            if (steps.Count > 1 || steps[0].ByDefender)
            {
                AlignColumns(panel);
                aligned = (panel, AlignFrames);
            }
            var line = $"COUNTER {defender.ToString()} answers={decided.Counters} " +
                       string.Join(" | ", sequence.Steps.Select(s => $"{(s.ByDefender ? "counter" : "strike")} {s.Numbers.Damage}/{s.Numbers.Killed} on {s.TargetHealth}"));
            if (line != lastDecision)
                Logs.Info(lastDecision = line);
        }
    }

    // ---------- game data ----------

    // A buff that forbids counters: a disabler for the counter ability (or every ability) and every attack type, or
    // the counter's own attack type. In the data: sleep, shackles, the space trap, medusa's stone, the unicorn's blind,
    // the assassin's debuff.
    static bool DisablesCounter(BuffData data, string counterType) =>
        data.disablers != null && data.disablers.Any(d => d != null && d.ability is "counter_attack" or "all" && (d.attack == "all" || d.attack == counterType));

    // ---------- the columns ----------

    // Each step after the first is a column in the panel's own rows, after the game's numbers: the marker (the game's
    // crossed swords from the unit plate for the defender's counter, a copy of the row's own icon for the attacker's
    // strike, sized against the row's icon) and the number: amber for the counter, white for the strike. A first step
    // by the defender (it answers first) has the game's numbers in amber.
    const string CounterColor = "#F2B544", StrikeColor = "#FFFFFF";
    const int MaxColumns = 3;   // counter, strike, counter, second strike: three after the first
    const float MarkScale = 0.7f;   // the marker against the row's icon
    const float MarkGap = 6f;       // between the number before and the marker, in row units
    const float NumberGap = 5f;     // between the marker and its number (fogone 06.11: it pressed onto the swords)
    Sprite swordsSprite;

    // The items of the column `i` (1..MaxColumns) in a row.
    static string Gap(int i) => $"OE_CounterGap{i}";
    static string Mark(int i) => $"OE_CounterMark{i}";
    static string MarkGapName(int i) => $"OE_CounterNumberGap{i}";
    static string Number(int i) => $"OE_CounterNumber{i}";
    static string ColumnPad(int i) => $"OE_CounterPad{i}";
    const string FirstPadName = "OE_CounterPad", EndPadName = "OE_CounterEndPad";

    void ShowSteps(BhHealDamageForecastPanel panel, List<Step> steps)
    {
        var hpRow = panel.hpPanel != null ? panel.hpPanel.transform : null;
        var killsRow = panel.stacksPanel != null ? panel.stacksPanel.transform : null;
        if (hpRow == null || killsRow == null)
            return;
        if (steps != null && steps.Any(s => s.ByDefender) && swordsSprite == null)
            swordsSprite = SwordsSprite(panel);
        var separator = Separator(hpRow);
        TintGameNumbers(hpRow, steps?[0].ByDefender == true);
        TintGameNumbers(killsRow, steps?[0].ByDefender == true);
        for (var i = 1; i <= MaxColumns; i++)
        {
            var step = steps != null && i < steps.Count ? steps[i] : null;
            RowColumn(hpRow, i, step?.Numbers.Damage.Text(separator), step?.ByDefender == true);
            RowColumn(killsRow, i, step is { Numbers.KilledMax: > 0 } ? step.Numbers.Killed.Text(separator) : null, step?.ByDefender == true);
        }
    }

    // The game's own numbers of a row in amber when they are the defender's counter (it answers first); the game writes
    // them again with its colour on its next show.
    static void TintGameNumbers(Transform row, bool counter)
    {
        if (!counter)
            return;
        foreach (var t in Enumerable.Range(0, row.childCount).Select(row.GetChild).Where(c => !c.name.StartsWith("OE_") && c.name != "Hyphen")
                     .SelectMany(c => c.GetComponentsInChildren<TMP_Text>(true)).Where(t => t.name == "Title"))
            if (!t.text.StartsWith("<color="))
                t.text = $"<color={CounterColor}>{t.text}</color>";
    }

    // The separator the game puts between a range's numbers in its forecast row: the text of the row's own "Hyphen"
    // item (fogone 06.11: the game writes a minus there, not a dash); a minus when the row has none.
    static string Separator(Transform row) =>
        row.Find("Hyphen")?.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text?.Trim()).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "-";

    // The sprite of the crossed swords on the unit plate the panel belongs to.
    Sprite SwordsSprite(BhHealDamageForecastPanel panel)
    {
        var plate = panel.GetComponentInParent<BhBattleUnitInfoView>(true);
        var sprite = plate?.counterAttackShowAnim?.GetComponentsInChildren<Image>(true).Select(i => i.sprite).FirstOrDefault(s => s != null);
        Logs.Once($"Counter mark sprite: {(sprite != null ? sprite.name : "not found yet, the number alone")}");
        return sprite;
    }

    // The column `i` of a row: copies of the row's own items as its last items, the gap ("Space"), the marker
    // ("IconShadow" with "Icon" under it) and the dash text ("Hyphen": a shadow text with the visible "Title" under it)
    // with the number. The row's layout group puts them after the numbers and the row's background stretches with the
    // row. Called for the columns in order, so they stand in order. `white`: the health row's numbers, health and not
    // damage (fogone 06.11).
    void RowColumn(Transform row, int i, string number, bool byDefender, bool white = false)
    {
        var shown = !string.IsNullOrEmpty(number);
        var withMark = !byDefender || swordsSprite != null;
        // The table's pads (AlignColumns) back to none: set again after all rows, or there is no column now.
        foreach (var name in new[] { FirstPadName, ColumnPad(i), EndPadName })
            if (row.Find(name)?.GetComponent<LayoutElement>() is { } pad)
                pad.minWidth = pad.preferredWidth = 0f;
        var gap = RowItem(row, "Space", Gap(i), shown && withMark);
        var mark = RowItem(row, "IconShadow", Mark(i), shown && withMark);
        var numberGap = RowItem(row, "Space", MarkGapName(i), shown && withMark);
        var label = RowItem(row, "Hyphen", Number(i), shown);
        if (!shown)
            return;
        if (mark != null)
        {
            // The swords for a counter; the row's own icon (its images in the same order) for a strike.
            var own = row.Find("IconShadow")?.GetComponentsInChildren<Image>(true);
            var images = mark.GetComponentsInChildren<Image>(true);
            for (var n = 0; n < images.Length; n++)
            {
                images[n].sprite = byDefender ? swordsSprite : own != null && n < own.Length ? own[n].sprite : images[n].sprite;
                if (images[n].transform != mark)
                    images[n].color = byDefender || own == null || n >= own.Length ? Color.white : own[n].color;
            }
            mark.localScale = new Vector3(MarkScale, MarkScale, 1f);
        }
        var tint = white || !byDefender ? StrikeColor : CounterColor;
        foreach (var t in label.GetComponentsInChildren<TMP_Text>(true))
        {
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.gameObject.SetActive(true);
            t.text = t.name == "Title" ? $" <color={tint}>{number}</color>" : $" {number}";
        }
        foreach (var item in new[] { gap, mark, numberGap, label })
            item?.SetAsLastSibling();
        // The drawn numbers stick out of their layout boxes on both sides, the more digits the further: each gap is
        // widened by what its neighbour number sticks out, so neither runs onto the marker.
        if (gap != null && gap.GetComponent<LayoutElement>() is { } spacing)
            spacing.minWidth = spacing.preferredWidth = Overhang(row, NumberBefore(row, i)) + MarkGap;
        if (numberGap != null && numberGap.GetComponent<LayoutElement>() is { } numberSpacing)
            numberSpacing.minWidth = numberSpacing.preferredWidth = Overhang(row, label) + NumberGap;
        LayoutRebuilder.MarkLayoutForRebuild(row.GetComponent<RectTransform>());
    }

    // The number before the column `i`: the nearest shown column's before it, else the game's last.
    static Transform NumberBefore(Transform row, int i)
    {
        for (var k = i - 1; k >= 1; k--)
            if (row.Find(Number(k)) is { } n && n.gameObject.activeSelf)
                return n;
        return Enumerable.Range(0, row.childCount).Select(row.GetChild)
            .LastOrDefault(c => c.gameObject.activeSelf && !c.name.StartsWith("OE_") && c.GetComponent<TMP_Text>() != null);
    }

    // The game lays its rows out again after the counter is decided (the next frames, the panel's appearing), and the
    // table would be off: it is made again for a few frames after (nothing moves while it stands).
    const int AlignFrames = 20;
    (BhHealDamageForecastPanel panel, int frames) aligned;

    void KeepAligned()
    {
        if (aligned.frames <= 0)
            return;
        aligned.frames--;
        if (aligned.panel == null || !aligned.panel.gameObject.activeInHierarchy)
        {
            aligned = default;
            return;
        }
        Logs.Guard("align", () => AlignColumns(aligned.panel));
    }

    // The forecast's rows as a table, a grid for any number of columns (fogone 06.11: built round the swords it did not
    // suit three): the rows' own icons in a column, each column's markers on one vertical, each column's numbers centred
    // in it, as wide as its widest. The game's rows are centred on one point and sized to what they hold (pivot 0.5, a size fitter,
    // MiddleCenter), so they line up when they are alike: measured inside each row (what is drawn: the numbers' text
    // sticks out of its layout box, the more digits the further), a pad after the row's icon gives the game's numbers
    // the widest one's room, a pad before each column puts its marker where the farthest row's is, and a pad at the end
    // makes the rows equally wide. Nothing fed back (moving by positions on the panel swung the centred rows off, 06.11).
    void AlignColumns(BhHealDamageForecastPanel panel)
    {
        var damage = panel.hpPanel != null ? panel.hpPanel.transform : null;
        if (damage == null)
            return;
        var rows = new[] { damage, panel.stacksPanel != null ? panel.stacksPanel.transform : null, damage.parent.Find(HealthRowName) }
            .Where(r => r != null && r.gameObject.activeInHierarchy)
            .Select(TableRow.Of)
            .Where(r => r != null)
            .ToList();
        if (!rows.Any(row => row.Columns.Any(c => c != null)))
            return;
        foreach (var row in rows)
        {
            Set(row.Pad, 0f);
            Set(row.EndPad, 0f);
            foreach (var column in row.Columns.Where(c => c != null))
                Set(column.Pad, 0f);
        }
        Rebuild(rows);
        // Inside each row: each marker after the number before it, its number after it. Earlier gaps move both alike,
        // so one pass measures them all.
        foreach (var row in rows)
        {
            var r = row.Rect;
            var before = row.Strike;
            foreach (var column in row.Columns.Where(c => c != null))
            {
                Set(column.Gap, Math.Max(0f, column.Gap.preferredWidth + MarkGap - (RectLeft(r, column.Mark) - DrawnRight(r, before))));
                Set(column.NumberGap, Math.Max(0f, column.NumberGap.preferredWidth + NumberGap - (DrawnLeft(r, column.Number) - RectRight(r, column.Mark))));
                before = column.Number;
            }
        }
        // Each column's numbers centred in the column, as wide as its widest: the game's by the pad after the row's icon,
        // ours by the gap after their marker (the markers are put on their verticals after this, from the left).
        var game = rows.ToDictionary(row => row, row => DrawnRight(row.Rect, row.Strike) - DrawnLeft(row.Rect, row.First));
        var widest = game.Values.Max();
        foreach (var row in rows)
            Set(row.Pad, (widest - game[row]) / 2f);
        for (var i = 0; i < MaxColumns; i++)
        {
            var numbers = rows.Where(row => row.Columns[i] != null)
                .ToDictionary(row => row, row => DrawnRight(row.Rect, row.Columns[i].Number) - DrawnLeft(row.Rect, row.Columns[i].Number));
            if (numbers.Count < 2)
                continue;
            var column = numbers.Values.Max();
            foreach (var (row, drawn) in numbers)
                Set(row.Columns[i].NumberGap, row.Columns[i].NumberGap.preferredWidth + (column - drawn) / 2f);
        }
        Rebuild(rows);
        // Each column's markers on one vertical: each row's pushed to where the farthest one stands.
        for (var i = 0; i < MaxColumns; i++)
        {
            var at = rows.Where(row => row.Columns[i] != null)
                .ToDictionary(row => row, row => RectLeft(row.Rect, row.Columns[i].Mark) - RectRight(row.Rect, row.Icon));
            if (at.Count < 2)
                continue;
            var farthest = at.Values.Max();
            foreach (var (row, x) in at)
                Set(row.Columns[i].Pad, farthest - x);
            Rebuild(rows);
        }
        // Equally wide rows: centred on one point, their columns meet.
        var width = rows.Max(row => row.Rect.rect.width);
        foreach (var row in rows)
            Set(row.EndPad, width - row.Rect.rect.width);
        Rebuild(rows);
        RefreshHealthBackground(panel);
    }

    static void Set(LayoutElement gap, float width)
    {
        if (gap != null)
            gap.minWidth = gap.preferredWidth = Math.Max(0f, width);
    }

    static void Rebuild(IEnumerable<TableRow> rows)
    {
        foreach (var row in rows)
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.Rect);
    }

    // One column of a row: the pad before it, its gap, its marker, the gap, its number.
    sealed record TableColumn(LayoutElement Pad, LayoutElement Gap, RectTransform Mark, LayoutElement NumberGap, Transform Number);

    // One row of the table: its icon, the pad after it, the game's last number, our columns (null: not shown in this
    // row), the pad at its end.
    sealed record TableRow(RectTransform Rect, RectTransform Icon, LayoutElement Pad, Transform First, Transform Strike, TableColumn[] Columns, LayoutElement EndPad)
    {
        public static TableRow Of(Transform row)
        {
            var children = Enumerable.Range(0, row.childCount).Select(row.GetChild).ToList();
            var first = children.FirstOrDefault(c => c.gameObject.activeSelf && !c.name.StartsWith("OE_") && c.GetComponent<TMP_Text>() != null);
            var strike = children.LastOrDefault(c => c.gameObject.activeSelf && !c.name.StartsWith("OE_") && c.GetComponent<TMP_Text>() != null);
            var icon = children.FirstOrDefault(c => c.gameObject.activeSelf && c.name == "IconShadow");
            if (strike == null || icon == null)
                return null;
            var pad = PadOf(row, FirstPadName);
            var endPad = PadOf(row, EndPadName);
            if (pad == null || endPad == null)
                return null;
            pad.SetSiblingIndex(icon.GetSiblingIndex() + 1);
            var columns = new TableColumn[MaxColumns];
            for (var i = 1; i <= MaxColumns; i++)
            {
                if (row.Find(Mark(i)) is not { } mark || !mark.gameObject.activeSelf)
                    continue;
                var gap = row.Find(Gap(i));
                var numberGap = row.Find(MarkGapName(i))?.GetComponent<LayoutElement>();
                var number = row.Find(Number(i));
                var columnPad = PadOf(row, ColumnPad(i));
                if (gap == null || numberGap == null || number == null || columnPad == null)
                    return null;
                columnPad.SetSiblingIndex(gap.GetSiblingIndex());
                columns[i - 1] = new TableColumn(columnPad.GetComponent<LayoutElement>(), gap.GetComponent<LayoutElement>(), mark.GetComponent<RectTransform>(),
                                                 numberGap, number);
            }
            endPad.SetAsLastSibling();
            return new TableRow(row.GetComponent<RectTransform>(), icon.GetComponent<RectTransform>(), pad.GetComponent<LayoutElement>(), first, strike, columns,
                                endPad.GetComponent<LayoutElement>());
        }
    }

    // The row's pad of that name: a copy of its own gap, shown; none when the row has no gap to copy.
    static Transform PadOf(Transform row, string name)
    {
        var pad = row.Find(name);
        if (pad == null && row.Find("Space") is { } space)
        {
            pad = UnityEngine.Object.Instantiate(space.gameObject, row, false).transform;
            pad.name = name;
            pad.GetComponent<LayoutElement>().minWidth = pad.GetComponent<LayoutElement>().preferredWidth = 0f;
        }
        pad?.gameObject.SetActive(true);
        return pad;
    }

    // The edges of a UI item as drawn (its scale too) and of a number's drawn text, in `space`'s coordinates.
    static float RectLeft(Transform space, RectTransform item) => space.InverseTransformPoint(item.TransformPoint(new Vector3(item.rect.xMin, 0f, 0f))).x;
    static float RectRight(Transform space, RectTransform item) => space.InverseTransformPoint(item.TransformPoint(new Vector3(item.rect.xMax, 0f, 0f))).x;

    static float DrawnLeft(Transform space, Transform item) => DrawnEdges(space, item).left;
    static float DrawnRight(Transform space, Transform item) => DrawnEdges(space, item).right;

    static (float left, float right) DrawnEdges(Transform space, Transform item)
    {
        float left = float.MaxValue, right = float.MinValue;
        foreach (var t in item.GetComponentsInChildren<TMP_Text>())
        {
            t.ForceMeshUpdate();
            var bounds = t.textBounds;
            left = Math.Min(left, space.InverseTransformPoint(t.transform.TransformPoint(new Vector3(bounds.min.x, 0f, 0f))).x);
            right = Math.Max(right, space.InverseTransformPoint(t.transform.TransformPoint(new Vector3(bounds.max.x, 0f, 0f))).x);
        }
        return left <= right ? (left, right) : (0f, 0f);
    }

    // How far the drawn text of a number item (shadow text with the visible "Title" under it) sticks out of the item's
    // layout box on each side, in row units.
    static float Overhang(Transform row, Transform item)
    {
        if (item == null || item.GetComponent<TMP_Text>() is not { } box)
            return 0f;
        var drawn = 0f;
        foreach (var t in item.GetComponentsInChildren<TMP_Text>())
        {
            t.ForceMeshUpdate();
            drawn = Math.Max(drawn, t.textBounds.size.x * Math.Abs(t.transform.lossyScale.x));
        }
        var boxWidth = box.preferredWidth * Math.Abs(item.lossyScale.x);
        return Math.Max(0f, (drawn - boxWidth) / 2f) / Math.Abs(row.lossyScale.x);
    }

    // The row's copy of its item `source`, made on first use; shown or hidden. Null when the row has no such item.
    static Transform RowItem(Transform row, string source, string name, bool active)
    {
        var item = row.Find(name);
        if (item == null)
        {
            if (!active || row.Find(source) is not { } original)
                return null;
            item = UnityEngine.Object.Instantiate(original.gameObject, row, false).transform;
            item.name = name;
        }
        item.gameObject.SetActive(active);
        return item;
    }
}
