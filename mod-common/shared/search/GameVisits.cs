using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.UI;
using Hex.UI.Tooltips;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nobirds.OldenEra.Modding.Common;

// Visits of map objects, their tooltip's texts and the map's outline: readable names for the game's members (Lookup,
// Members; found once, said in the log). The comments name each member as it is on 0.82.7.
public static partial class Game
{
    // The game's key of the "already visited" line of a map object's tooltip (ui.json, "tooltipVisited"). The tooltip's
    // fillers (cwt.wdx for event banks, cwx for other map objects) put its text when VisitedBy says so, an empty string
    // when not.
    public const string TooltipVisitedKey = "tooltipVisited";

    // BhTooltip's void M(BasicTooltip, options) (vzp): puts the texts (header, sub-header, content, extra) into the
    // tooltip; the fillers call it with the texts they made.
    static readonly Lookup<MethodInfo> tooltipSetTexts = Lookup<MethodInfo>.Among("tooltip set texts",
        () => typeof(BhTooltip).Methods().Instance().Returns(typeof(void)).Takes(typeof(BasicTooltip), null), Name, onlyOne: true).Generate("SetTexts").GenerateTarget("TooltipSetTexts");


    // The options the texts are set with (crv on 0.82.7): the set-texts method's second parameter.
    static readonly Lookup<Type> tooltipOptions = Lookup<Type>.Among("tooltip text options",
        () => tooltipSetTexts.Value is { } m ? new[] { m.GetParameters()[1].ParameterType } : null, t => t.Name).Generate("GameTooltipOptions");

    // The map object's virtual bool M(side wrapper) (bnpc, declared by the map object's logic fql, its own versions in
    // the event bank fpk and fpm): the game's "already visited" for a player, the check its tooltip writes the visited
    // line by. Of the map object's bool M(side wrapper), the one its subclasses have their own versions of.
    static readonly Lookup<MethodInfo> visitedBy = new Lookup<MethodInfo>("map object visited by side", () =>
    {
        if (MapObjectType is not { } mapObject || SideOwnerType is not { } side)
            return null;
        var subclasses = mapObject.Subclasses().ToList();
        bool Overridden(MethodInfo m) => subclasses.Methods().Returns(typeof(bool)).Takes(side).NamedInGame(m.NameInGame()).Any();
        return mapObject.Methods().Instance().Returns(typeof(bool)).Takes(side).Where(Overridden).OnlyOne();
    }, Name).Generate("VisitedBy");

    // A map object's configs (ObjConfigBase: its visit type, its period): the DataObject's properties of that type (cpuu,
    // cwsp on 0.82.7), all of them (data.Configs).
    static readonly Lookup<PropertyInfo> objectConfigs = Lookup<PropertyInfo>.Among("map object data -> configs",
        () => typeof(Hex.Session.Data.DataObject).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Hex.Configs.ObjConfigBase)), Members.Name).GenerateAll("Configs");

    // The id a hero is kept by in a building's visitors list (ObjEventBank.visitorsList of a per-hero building): the
    // only int property of Hex.Session.Logic.Hero (cwfa). The game's own getter of it is the hero's int M() its event
    // bank's per-hero check calls (fgd.blsy); both were 0 for the hero checked (04.11.2026, one hero only).
    static readonly GameProperty<Hex.Session.Logic.Hero, int> visitId = new GameProperty<Hex.Session.Logic.Hero, int>("hero visit id", _ => true).Generate("VisitId");

    // The game's handing of the map outline's colors to the shader: static void M(CommandBuffer, int[] color index per
    // tag, Color[] palette) (bei.lkv; cfk and dhb are clones), every frame. The palette's colors are chosen in the
    // game's settings; the color of a tag is palette[index[tag]] (Hex.Render.SelectionOutlineTag).
    static readonly Lookup<List<MethodInfo>> outlineColors = new Lookup<List<MethodInfo>>("outline colors to the shader",
        () => Types.Methods().Static().Returns(typeof(void))
            .Takes(typeof(CommandBuffer), typeof(Il2CppStructArray<int>), typeof(Il2CppStructArray<Color>)).ToList() is { Count: > 0 } found ? found : null,
        Members.Names).GenerateTarget("OutlineColorsToShader");

}
