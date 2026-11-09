using System.Linq;
using System.Reflection;
using Hex.MainMenu;

namespace OldenEra.Modding.Common;

// The game's main menu (issue #24): its screen (ScMainMenu.me) holds the view with every button and sub-menu in a field
// of its own; the view wires them one by one (MainMenuNavigationView.qjf). A top-level button's handler (the settings
// one, qjc) first clears the menu's selection, which also closes an open sub-menu. The comments name each member as it
// is on 0.82.7.
internal static partial class Game
{
    // Select a button of the menu, null for none (MainMenuNavigationView.qjp(cab)): the view's only bool M(cab); with
    // another button it closes the open sub-menu.
    static readonly Lookup<MethodInfo> menuSelect = Lookup<MethodInfo>.Among("main menu select",
        () => typeof(MainMenuNavigationView).Methods().Instance().Returns(typeof(bool))
            .Where(m => m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType.IsInterface == false && ps[0].ParameterType.Assembly == typeof(MainMenuNavigationView).Assembly &&
                        !ps[0].ParameterType.IsValueType)
            .DistinctCode(),
        Members.Name, onlyOne: true).Generate("Select");

    // What the menu selects (cab): its buttons, each a selectable of its own.
    static readonly Lookup<System.Type> menuSelectable = Lookup<System.Type>.Among("main menu selectable",
        () => menuSelect.Value is { } select ? new[] { select.GetParameters()[0].ParameterType } : null, t => t.Name).Generate("GameMenuSelectable");

    // The open sub-menu (MainMenuNavigationView.ccbs), the one of its sub-menu fields with no name of the scene's.
    static readonly Lookup<PropertyInfo> menuOpenSubMenu = Lookup<PropertyInfo>.Among("main menu open sub-menu",
        () => DeclaredProperties(typeof(MainMenuNavigationView)).Where(p => p.PropertyType == typeof(MainMenuSubNavView) && !p.Name.EndsWith("SubMenu")),
        Name, onlyOne: true).Generate("OpenSubMenu");
}
