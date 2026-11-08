using Hex.Session.Data;
using Hex.UI;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.Visited;

// How dangerous the fight for a guarded building is, all done by the game's skulls widget (BhSkulls): its
// int M(DataParty) (umo on 0.82.7, skulls.FillFor(party)) fills the widget for an enemy party, against the selected
// hero's army, the way the dwelling tooltip shows its guard, and returns the danger level; M(int) -> Color (umq,
// skulls.DangerColor(level)) gives the level's colour. The tooltip of any other guarded building gets the squad tooltip's
// skulls filled so; a hidden copy of the widget tells whether the game's widget is there yet.
public partial class Plugin
{
    const string SkullsName = "OE_VisitSkulls";
    BhSkulls shownSkulls;    // a tooltip's widget the plugin turned on

    // Our hidden copy of the widget; null while there is no widget to copy.
    BhSkulls HiddenSkulls() =>
        Game.HiddenSkulls(SkullsName);

    // The tooltip's skulls, filled by the game for the guard (a squad tooltip shown for a building leaves them off).
    void ShowSkulls(BhSkulls widget, DataParty guard)
    {
        if (widget == null || guard == null || widget.gameObject.activeSelf || HiddenSkulls() == null)
            return;
        widget.ShowDangerOfParty(guard);
        widget.gameObject.SetActive(true);
        shownSkulls = widget;
    }

    void HideSkulls()
    {
        if (shownSkulls != null)
            shownSkulls.gameObject.SetActive(false);
        shownSkulls = null;
    }
}
