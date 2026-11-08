using System;
using System.Globalization;
using System.Linq;
using Hex.UI.GeneralKit;
using OldenEra.Modding.Common;
using static OldenEra.Modding.Common.UiLayout;
using UnityEngine;

namespace OldenEra.Modding.Settings;

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
    int pendingAt;

    void FillSources()
    {
        foreach (var source in ModCatalog.Sources)
        {
            var row = Copy(rowSource, listContent);
            row.name = "OE_Source";
            row.SetActive(true);
            var url = source.Url;
            RowBase(row, sourcePick == url, () => sourcePick = url);
            row.transform.Find("Toggle")?.gameObject.SetActive(false);
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
        InfoPair(W + "catalogLabel", Texts.Get(source.TitleKey), source.IconPath);
        SetPair(versionLabel, versionValue, Texts.Get(W + "modsLabel"), source.Mods.ToString());
        SetPair(originLabel, originValue, Texts.Get(W + "checkedLabel"), source.LastChecked is { } when ? Moment(when) : Texts.Get(W + "checkedNever"));
        InfoText($"<color=#A5A5A5>{source.Url}</color>");
        if (source.BuiltIn)
            InfoLine(Texts.Get(W + "builtIn"));
        if (source.NoteKey != null)
            InfoText(Texts.Get(source.NoteKey));
        if (source.SetCheckEvery is { } setEvery && dropdownSource != null)
        {
            InfoLine(Texts.Get(W + "every"));
            var holder = NewRect("OE_Every", infoContent);
            Size(holder.gameObject, 440f, 40f);
            // Laid out from the left by the holder (the dropdown's own anchors are its row's).
            var everyRow = HRow(holder.gameObject, 0f, TextAnchor.MiddleLeft);
            everyRow.childControlWidth = everyRow.childControlHeight = false;
            var dropdown = Copy(dropdownSource, holder).GetComponent<BhDropdown>();
            dropdown.gameObject.SetActive(true);
            BindDropdown(dropdown, i =>
            {
                if (i >= 0 && i < CheckEveryValues.Length)
                    Guard("check every", () => setEvery(CheckEveryValues[i]));
            });
            GameUi.SetDropdownTexts(dropdown, Enumerable.Range(0, CheckEveryValues.Length).Select(i => Texts.Get($"{W}every.{i}")),
                Math.Max(0, Array.IndexOf(CheckEveryValues, source.CheckEvery)));
        }
        PanelButton(primaryButton, W + "check", source.Check != null, () => source.Check?.Invoke());
        PanelButton(deleteButton, null, source.Remove != null && !source.BuiltIn, () => source.Remove?.Invoke());
        PanelButton(secondaryButton, null, false, null);
    }

    // ---------- the game's boxes: adding a source ----------

    // First the game's own message box warning that nobody reviews other sources, then its input box for the address;
    // they come over our window (PlaceCanvas), which waits under them.
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

    // A game box is open over the window: the window stays (drawn under it, PlaceCanvas), only its own Escape waits;
    // when the window is drawn over everything (no camera to share), it hides meanwhile.
    void SuspendWindow()
    {
        suspended = true;
        suspendedAt = Time.frameCount;
        if (window != null && window.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay)
            window.SetActive(false);
    }

    void ResumeWindow()
    {
        if (!suspended || window == null)
            return;
        suspended = false;
        resumedAt = Time.frameCount;
        window.SetActive(true);
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
