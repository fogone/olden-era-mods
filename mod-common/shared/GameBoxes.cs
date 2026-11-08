using System;
using Hex.UI.GeneralKit;
using Hex.UI;

namespace OldenEra.Modding.Common;

// The game's own message box (a question with two buttons) and input box (a line of text), opened with the game's
// options objects (GameWidgets.cs). The texts are ours (already translated); the callbacks run on the main thread.
//
//   GameBoxes.Confirm(Texts.Get("oe.x.title"), Texts.Get("oe.x.text"), Texts.Get("oe.x.go"), cancel, onConfirm: Go, onCancel: null);
//   GameBoxes.Ask(Texts.Get("oe.x.caption"), Texts.Get("oe.x.placeholder"), ok, cancel, onConfirm: text => Add(text), onCancel: null);
internal static class GameBoxes
{
    public static void Confirm(string caption, string text, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
    {
        var options = new GameMessageBoxOptions();
        options.Caption = caption;
        options.Text = text;
        var confirm = new GameMessageBoxButton();
        confirm.Label = confirmLabel;
        confirm.Click = (Il2CppSystem.Action)(onConfirm ?? (() => { }));
        var cancel = new GameMessageBoxButton();
        cancel.Label = cancelLabel;
        cancel.Click = (Il2CppSystem.Action)(onCancel ?? (() => { }));
        options.Confirm = confirm;
        options.Cancel = cancel;
        ScMessageBox.Open(options);
    }

    // `onConfirm` gets the text typed as it is.
    public static void Ask(string caption, string placeholder, string confirmText, string cancelText, Action<string> onConfirm, Action onCancel)
    {
        var options = new GameInputBoxOptions();
        options.Caption = caption;
        options.Placeholder = placeholder;
        options.ConfirmText = confirmText;
        options.CancelText = cancelText;
        options.OnConfirm = (Il2CppSystem.Action<string>)(onConfirm ?? (_ => { }));
        options.OnCancel = (Il2CppSystem.Action)(onCancel ?? (() => { }));
        ScInputBox.Open(options);
    }

    // Whether the message box or the input box is on screen (they close on the game's Escape without a callback).
    public static bool AnyShown() => Shown(Scene.Find<ScMessageBox>()) || Shown(Scene.Find<ScInputBox>());

    static bool Shown(BhScreen screen) => screen != null && screen.gameObject.activeInHierarchy && (screen.cg == null || screen.cg.alpha > 0.01f);
}
