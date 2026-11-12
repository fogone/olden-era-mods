using System;
using Hex.MainMenu;
using Hex.UI.GeneralKit;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// A button of ours in the game's main menu (issue #24). The menu is the game's screen ScMainMenu (in its screen
// registry); its view holds every button and sub-menu in a field of its own and has no list to add to, and its buttons
// have no prefab (each is a copy in the scene). So ours is a copy of one of its buttons, put in its column, clicked as its own
// top-level buttons are: the menu's selection cleared and an open sub-menu closed first.
//
//   OnTick(() =>
//   {
//       if (button == null)   // a new main menu (after a game) has none: made again
//           button = MainMenu.AddButton("OE_MyButton", "oe.my.button", Open, view => view.additionallyButton);
//   });
//   ...
//   MainMenu.Remove(button);   // on unload
public static class MainMenu
{
    // The menu's view (its buttons and sub-menus), null outside the main menu. The menu screen is the one registered
    // under its name, as ScMainMenu.Open and Hide find it (ScMainMenu.me is no longer set on 0.82.09); the registry's
    // own "screen by name" logs an error for a missing one, so its dictionary is read.
    public static MainMenuNavigationView View =>
        GameNames.ScreenRegistry() is { } registry && registry.Screens is { } screens &&
        screens.TryGetValue(nameof(ScMainMenu), out var screen) && screen != null && screen.TryCast<ScMainMenu>() is { } menu
            ? menu.mainMenuNavigationView
            : null;

    // A copy of the menu's Load button named `name`, labelled by our localization key, under `after`'s button in its
    // column (below its sub-menu, if one follows it); one left by a reloaded build is replaced. Null when there is no
    // main menu.
    public static BhNavigationButton AddButton(string name, string labelKey, Action click, Func<MainMenuNavigationView, BhNavigationButton> after)
    {
        var view = View;
        var template = view?.loadButton;
        var anchor = view != null ? after(view) : null;
        if (template == null || anchor == null)
            return null;
        var column = anchor.transform.parent;
        for (var i = column.childCount - 1; i >= 0; i--)
            if (column.GetChild(i).name == name)
                UnityEngine.Object.DestroyImmediate(column.GetChild(i).gameObject);
        var place = anchor.transform.GetSiblingIndex() + 1;
        if (place < column.childCount && column.GetChild(place).GetComponent<MainMenuSubNavView>() != null)
            place++;

        var copy = UnityEngine.Object.Instantiate(template.gameObject, column, false);
        copy.name = name;
        copy.transform.SetSiblingIndex(place);
        var button = copy.GetComponent<BhNavigationButton>();
        GameUi.Label(button.label, labelKey);
        button.MenuClick = (Il2CppSystem.Action)(() =>
        {
            if (View is { } menu)
            {
                menu.Select(null);
                if (menu.OpenSubMenu is { } open && open != null)
                    open.Hide();
            }
            click();
        });
        return button;
    }

    public static void Remove(BhNavigationButton button)
    {
        if (button != null)
            UnityEngine.Object.DestroyImmediate(button.gameObject);   // at once: a reloaded build adds its own in the same frame
    }
}
