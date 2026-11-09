using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.UI.Tooltips;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// The game's tooltips: readable names for their members (Lookup, Members; found once, said in the log). The comments
// name each member as it is on 0.82.7.
public static partial class Game
{
    // ---------- the tooltip's positioner ----------

    // The tooltip's positioner (BhTooltip.cfsf, of type cxg): places the tooltip on screen. It has the camera, turns a
    // world point into a place on the canvas (void M(Vector3): wgf, fkj), keeps it on screen, snaps to UI elements.
    static readonly Lookup<PropertyInfo> tooltipPositioner = Lookup<PropertyInfo>.Among("tooltip -> positioner",
        () => Properties(typeof(BhTooltip)).Where(p => HasProperty(p.PropertyType, typeof(Camera)) &&
                                                      p.PropertyType.Methods().Instance().Returns(typeof(void)).Takes(typeof(Vector3)).Any()),
        p => $"{Name(p)} ({p.PropertyType.Name})", onlyOne: true).Generate("Positioner", "GameTooltipPositioner");

    public static Type TooltipPositionerType => tooltipPositioner.Value?.PropertyType;

    // The positioner's virtual Vector2 M(Vector2) (wdp): the tooltip's place on the canvas for the object's point there
    // (from the canvas centre); the point methods call it, then set the tooltip there and keep it inside the screen.
    // The positioners are of subclasses with their own versions (cwn for map objects, cwr for book pages): all of them.
    static readonly Lookup<List<MethodInfo>> placeForPoint = new Lookup<List<MethodInfo>>("positioner place for point", () =>
    {
        if (TooltipPositionerType is not { } type || type.Methods().Instance().Returns(typeof(Vector2)).Takes(typeof(Vector2)).OnlyOne() is not { } own)
            return null;
        return type.Subclasses().Methods().Named(own.Name).Returns(typeof(Vector2)).Takes(typeof(Vector2)).Prepend(own).ToList();
    }, Members.Names).GenerateTarget("TooltipPlaceForPoint");

    // ---------- the game's text tooltip ----------

    // The tooltips' holder, one in the game: BhTooltips' static BhTooltips (cftf on 0.82.7).
    static readonly Lookup<PropertyInfo> tooltips = Lookup<PropertyInfo>.Among("tooltips holder",
        () => typeof(BhTooltips).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(BhTooltips)),
        Name, onlyOne: true).Generate("Tooltips");

    // Its basic tooltip of texts (wbt on 0.82.7): void M(header, content, extra, RectTransform at, options, ESnapDirection,
    // bool), the texts already in the game's language; a hovered unit buff shows its name and description so
    // (BhUnitWindowBuff.OnHover: wbt(name, description, null, rect, null, Top, false)). Wraps its lines.
    static readonly Lookup<MethodInfo> textTooltip = Lookup<MethodInfo>.Among("text tooltip",
        () => tooltipOptions.Value is { } options
            ? typeof(BhTooltips).Methods().Instance().Returns(typeof(void))
                .Takes(typeof(string), typeof(string), typeof(string), typeof(RectTransform), options, typeof(ESnapDirection), typeof(bool))
            : null,
        Members.Name, onlyOne: true).Generate("ShowTexts");
}
