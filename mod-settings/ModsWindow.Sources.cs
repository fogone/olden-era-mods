using System;
using System.Globalization;
using System.Linq;
using Hex.UI.GeneralKit;
using Nobirds.OldenEra.Modding.Common;
using static Nobirds.OldenEra.Modding.Common.UiLayout;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Settings;

// The Mods window's Sources tab: the sources' rows and details, and adding one through the game's own message box (a
// warning) and input box (the address).
public partial class Plugin
{
    // The values of a source's "check every" setting, in the order of its dropdown's options (every.0 ... every.3).
    static readonly string[] CheckEveryValues = { "start", "daily", "weekly", "manual" };

    // A game box (the warning, the input box) is open: our window waits under it.
    bool suspended;
    int suspendedAt, resumedAt = -10;
    // The input box, opened a moment after the warning closes.
    Action pendingBox;
    // The manager's failures told so far (ModCatalog.ProblemRevision).
    int shownProblem;
    int pendingAt;

    void FillSources()
    {
        foreach (var source in ModCatalog.Sources)
        {
            var url = source.Url;
            var row = NewRow("OE_Source", sourcePick == url, () => sourcePick = url);
            if (row == null)
                continue;
            RowPicture(row, source.IconPath, Texts.Get(source.TitleKey));
            RowName(row, Texts.Get(source.TitleKey), source.BuiltIn ? new[] { W + "builtIn" } : null);
            RowInfo(row, string.Format(Texts.Get(W + "mods"), source.Mods),
                source.LastChecked is { } when ? string.Format(Texts.Get(W + "checked"), Moment(when)) : Texts.Get(W + "checkedNever"),
                source.NoteKey != null ? Texts.Get(source.NoteKey) : "");
        }
        ShowNothing(ModCatalog.Sources.Count == 0, W + "nothing");
        BottomButton(W + "addUrl", ModCatalog.AddSource != null, () => AskForSource(W + "url.caption", W + "url.placeholder", ModCatalog.AddSource), 260f);
        BottomButton(W + "addGitHub", ModCatalog.AddGitHubSource != null, () => AskForSource(W + "github.caption", W + "github.placeholder", ModCatalog.AddGitHubSource), 260f);
        BottomButton(W + "checkAll", ModCatalog.CheckAll != null, () => ModCatalog.CheckAll?.Invoke(), 220f);
    }

    static string Moment(DateTime when) => when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    void SourceDetails(CatalogSource source)
    {
        if (panelTitle != null)
            SetText(panelTitle, Texts.Get(source.TitleKey));
        ShowSquares();
        // The boxes: how many mods, and the catalog's icon by its last check (as the catalog's box of a mod).
        SetPair(versionLabel, versionValue, Texts.Get(W + "modsLabel"), source.Mods.ToString());
        SetPair(originLabel, originValue, Texts.Get(W + "checkedLabel"), source.LastChecked is { } when ? Moment(when) : Texts.Get(W + "checkedNever"));
        BoxIcon(Picture(source.IconPath));
        InfoText(PairText(W + "url.caption", source.Url));
        // What the source is and how its last reading went, on one muted line.
        var state = new[] { source.BuiltIn ? Texts.Get(W + "builtIn") : null, source.NoteKey != null ? Texts.Get(source.NoteKey) : null }.Where(s => s != null).ToList();
        if (state.Count > 0)
            InfoText(Coloured(string.Join(" · ", state), versionLabel));
        if (source.SetCheckEvery is { } setEvery && dropdownSource != null)
        {
            // "Check for updates:" and the game's dropdown on one row.
            var holder = InfoRow("OE_Every", 44f);
            var everyRow = HRow(holder.gameObject, 12f, TextAnchor.MiddleLeft);
            everyRow.childControlWidth = everyRow.childControlHeight = false;
            var labelWidth = AddLine(holder, PairText(W + "every", ""));
            var dropdown = Copy(dropdownSource, holder).GetComponent<BhDropdown>();
            dropdown.gameObject.SetActive(true);
            // The rest of the row (it would run past the panel's edge at its own width).
            var dropdownRect = dropdown.transform.Cast<RectTransform>();
            dropdownRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Math.Min(dropdownRect.rect.width, holder.sizeDelta.x - labelWidth - 12f - 16f));
            BindDropdown(dropdown, i =>
            {
                if (i >= 0 && i < CheckEveryValues.Length)
                    Guard("check every", () => setEvery(CheckEveryValues[i]));
            });
            GameUi.SetDropdownTexts(dropdown, Enumerable.Range(0, CheckEveryValues.Length).Select(i => Texts.Get($"{W}every.{i}")),
                Math.Max(0, Array.IndexOf(CheckEveryValues, source.CheckEvery)));
        }
        PanelButton(primaryButton, W + "check", source.Check != null, () => source.Check?.Invoke());
        PanelButton(deleteButton, null, source.Remove != null && !source.BuiltIn, () => RemoveSource(source));
        PanelButton(secondaryButton, null, false, null);
    }

    // ---------- the game's boxes: adding a source ----------

    // First the game's own message box warning that nobody reviews other sources, then its input box for the address;
    // they come over our window (drawn just under them), which waits.
    void AskForSource(string captionKey, string placeholderKey, Action<string> add)
    {
        if (add == null)
            return;
        SuspendWindow();
        GameBoxes.Confirm(Texts.Get(W + "warn.title"), Texts.Get(W + "warn.text"), Texts.Get(W + "warn.go"), GameText("button_cancel", W + "cancel"),
            () => Guard("warning confirmed", () =>
            {
                // The input box a moment later: the message box closes first.
                pendingBox = () => OpenInputBox(captionKey, placeholderKey, add);
                pendingAt = Time.frameCount + 20;
                suspendedAt = Time.frameCount + 20;
            }),
            () => Guard("warning cancelled", ResumeWindow));
    }

    void OpenInputBox(string captionKey, string placeholderKey, Action<string> add)
    {
        SuspendWindow();
        GameBoxes.Ask(Texts.Get(captionKey), Texts.Get(placeholderKey), Texts.Get(W + "add"), GameText("button_cancel", W + "cancel"),
            text => Guard("source typed", () =>
            {
                ResumeWindow();
                if (!string.IsNullOrWhiteSpace(text))
                    add(text.Trim());
            }),
            () => Guard("input cancelled", ResumeWindow));
    }

    // The manager's latest failure (a source not read or not added, an action failed) in the game's message box, once;
    // one that happened before the window opened is told when it opens.
    void TellProblem()
    {
        shownProblem = ModCatalog.ProblemRevision;
        SuspendWindow();
        GameBoxes.Tell(Texts.Get(W + "problem"), Texts.Get(ModCatalog.ProblemKey), GameText("button_ok", W + "ok"),
            () => Guard("problem told", ResumeWindow));
    }

    // A source with installed mods: the game's message box asks whether they go with it; kept, they stay installed
    // ("source removed", no updates).
    void RemoveSource(CatalogSource source)
    {
        if (source.Remove == null)
            return;
        if (source.Installed == 0)
        {
            source.Remove(false);
            return;
        }
        SuspendWindow();
        GameBoxes.Confirm(Texts.Get(W + "srcRemove.title"), string.Format(Texts.Get(W + "srcRemove.text"), source.Installed),
            Texts.Get(W + "srcRemove.withMods"), Texts.Get(W + "srcRemove.keepMods"),
            () => Guard("source removed with its mods", () => { source.Remove(true); ResumeWindow(); }),
            () => Guard("source removed, its mods kept", () => { source.Remove(false); ResumeWindow(); }));
    }

    // A game box is open over the window: the window stays (drawn under it), its Escape and filling wait.
    void SuspendWindow()
    {
        suspended = true;
        suspendedAt = Time.frameCount;
    }

    void ResumeWindow()
    {
        if (!suspended || window == null)
            return;
        suspended = false;
        resumedAt = Time.frameCount;
        windowDirty = true;
    }

    // A box closed without telling us (the game's Escape, or a scene change): the window comes back once neither box
    // is shown for a while.
    void WatchGameBoxes()
    {
        if (pendingBox != null || Time.frameCount < suspendedAt + 60)
            return;
        if (GameBoxes.AnyShown())
        {
            suspendedAt = Time.frameCount;
            return;
        }
        ResumeWindow();
    }

    // A text of the game's own (its Cancel), ours when it has none.
    static string GameText(string gameKey, string ownKey)
    {
        string text = null;
        try { text = Game.Translate(gameKey); }
        catch (Exception) { }
        return string.IsNullOrEmpty(text) || text == gameKey ? Texts.Get(ownKey) : text;
    }
}
