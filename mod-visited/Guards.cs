using System.Linq;
using System.Reflection;
using Hex.Session.Data;
using Hex.UI;
using Hex.UI.Tooltips;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.Visited;

// Who guards a building (a unit dwelling, a bank): the tooltip of a guarded map object gets the game's own garrison
// row (BhTooltipGarnison, the one a squad's tooltip shows), filled with the object's guard party the way the game
// fills it, so no need to walk up and decline the battle to see it. The game's way of showing a squad's size: words
// like "a few", not numbers, as the game's own fills ask for them.
public partial class Plugin
{
    Switch showGuards;
    BhTooltipGarnison shownGarrison;

    // The garrison row's fill (BhTooltipGarnison.unj(party, bool) on 0.82.7, garrison.FillWith) and the map object's party
    // wrapper it takes (fql.crkq, mapObject.Party): the generated names.
    // A unit dwelling has its own tooltip (BhTooltipHire: the unit to hire, danger skulls, a garrison row the game
    // leaves hidden), filled for the dwelling (ModEvents.DwellingTooltipFilled; wae(fpq) on 0.82.7).
    void OnDwellingTooltipFilled(DwellingTooltipFill fill)
    {
        if (fill.Dwelling.Typed is { } mapObject && mapObject.ObjectData is { } data)
            ShowGuardsIn(fill.Tooltip.garnison, mapObject, data);
    }

    // Before the game fills the next tooltip: our garrison row goes away, the game shows its own where it wants.
    void HideGuards()
    {
        if (shownGarrison != null)
            shownGarrison.gameObject.SetActive(false);
        shownGarrison = null;
        HideSkulls();
    }

    void ShowGuards(BhTooltipSquad tooltip, GameMapObject mapObject, DataObject data)
    {
        ShowGuardsIn(tooltip.container.garnison, mapObject, data);
        if (shownGarrison != null)
            ShowSkulls(tooltip.container.skulls, data.garnisonParty);
    }

    void ShowGuardsIn(BhTooltipGarnison garrison, GameMapObject mapObject, DataObject data)
    {
        if (!showGuards.On || garrison == null || garrison.gameObject.activeSelf)
            return;
        var units = data.garnisonParty?.units;
        if (units == null || units.Count == 0)
            return;
        var side = Game.SelectedHero?.HeroSide;
        if (side != null && data.ownerSide == side.index)
            return;
        if (mapObject.Party is not { } party)
            return;
        garrison.gameObject.SetActive(true);
        shownGarrison = garrison;
        // As the game fills it for a building not the viewer's (BhTooltipHire.wae: unj(party, true); cwx.weo: true unless
        // the owner is the viewer's ally): the sizes in words, not numbers.
        garrison.FillWith(party, true);
    }
}
