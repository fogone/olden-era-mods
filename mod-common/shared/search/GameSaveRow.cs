using System.Linq;
using System.Reflection;
using Hex.UI.SaveLoad;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// The Load screen's save row (BhSessionInfoElement, issue #24): its prefab is the asset the screen's list makes rows of
// (BhVirtualizedList.sessionPrefab). The row shows what it is given through setters of its own; its presenter (made in
// Awake) wires the pointer events. The comments name each member as it is on 0.82.7.
public static partial class Game
{
    // Its picture (vff(Sprite)): the session icon's sprite.
    static readonly Lookup<MethodInfo> saveRowIcon = Lookup<MethodInfo>.Among("save row icon",
        () => typeof(BhSessionInfoElement).Methods().Instance().Returns(typeof(void)).Takes(typeof(Sprite)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("ShowIcon");

    // Its tag chip i: the text (the chip sized to it) and the colour (vfh(int, string, Color)).
    static readonly Lookup<MethodInfo> saveRowTag = Lookup<MethodInfo>.Among("save row tag",
        () => typeof(BhSessionInfoElement).Methods().Instance().Returns(typeof(void)).Takes(typeof(int), typeof(string), typeof(Color)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("ShowTag");

    // Its tag chip i shown or hidden (vfi(int, bool)).
    static readonly Lookup<MethodInfo> saveRowTagShown = Lookup<MethodInfo>.Among("save row tag shown",
        () => typeof(BhSessionInfoElement).Methods().Instance().Returns(typeof(void)).Takes(typeof(int), typeof(bool)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("ShowTagShown");

    // Its selected look (vfp): the selected background.
    static readonly Lookup<MethodInfo> saveRowSelected = Lookup<MethodInfo>.Among("save row selected look",
        () => BoolSetters(typeof(BhSessionInfoElement), nameof(BhSessionInfoElement.selectedBackground)), Members.Name, onlyOne: true)
        .Generate("ShowSelected");

    // The cloud mark (vfe): in the cloud or not; one of its two pictures is always shown.
    static readonly Lookup<MethodInfo> saveRowCloud = Lookup<MethodInfo>.Among("save row cloud",
        () => BoolSetters(typeof(BhSessionInfoElement), nameof(BhSessionInfoElement.cloudOn), nameof(BhSessionInfoElement.cloudOff)), Members.Name, onlyOne: true)
        .Generate("ShowInCloud");

    // The campaign's information shown or hidden (vfk).
    static readonly Lookup<MethodInfo> saveRowCampaign = Lookup<MethodInfo>.Among("save row campaign shown",
        () => BoolSetters(typeof(BhSessionInfoElement), nameof(BhSessionInfoElement.campaignInfoRoot)), Members.Name, onlyOne: true)
        .Generate("ShowCampaign");

    // The map's icons (mode, size, players) shown or hidden (vfn).
    static readonly Lookup<MethodInfo> saveRowMapIcons = Lookup<MethodInfo>.Among("save row map icons shown",
        () => BoolSetters(typeof(BhSessionInfoElement), nameof(BhSessionInfoElement.infoIcons)), Members.Name, onlyOne: true)
        .Generate("ShowMapIcons");

    // Its presenter (vfc(): made on first use, as Awake does): the pointer events, the hover look, the clicks.
    static readonly Lookup<MethodInfo> saveRowPresenter = Lookup<MethodInfo>.Among("save row presenter",
        () => typeof(BhSessionInfoElement).Methods().Instance().Where(m => m.GetParameters().Length == 0 && m.ReturnType.IsClass &&
                                                                          m.ReturnType.Assembly == typeof(BhSessionInfoElement).Assembly && m.ReturnType.Namespace == null)
            .DistinctCode(),
        Members.Name, onlyOne: true).Generate("Presenter");

    static readonly Lookup<System.Type> saveRowPresenterType = Lookup<System.Type>.Among("save row presenter type",
        () => saveRowPresenter.Value is { } m ? new[] { m.ReturnType } : null, t => t.Name).Generate("GameSaveRowPresenter");

    // The presenter's two actions (cum +0x38, +0x40): the first is called on a pointer event of the row's background
    // (the click), the second when its check box changes.
    static System.Collections.Generic.List<PropertyInfo> PresenterActions() =>
        saveRowPresenterType.Value is { } t ? FieldsInOrder(t, typeof(Il2CppSystem.Action), 2) : null;

    // What a click on the row does (fkf(Action), sets +0x38; one native code with many setters of a field at +0x38).
    static readonly Lookup<MethodInfo> saveRowOnClick = Lookup<MethodInfo>.Among("save row on click",
        () => saveRowPresenterType.Value is { } t && PresenterActions() is { } actions && GameCode.FieldOf(actions[0])?.offset is { } at
            ? t.Methods().Instance().Returns(typeof(void)).Takes(typeof(Il2CppSystem.Action)).Where(m => GameCode.Offsets(m).Contains(at)).DistinctCode()
            : null,
        Members.Name, onlyOne: true).Generate("OnClick");

    // The row's name: the name, and a second one after the game's separator when not empty (ifs(string, string)).
    static readonly Lookup<MethodInfo> saveRowName = Lookup<MethodInfo>.Among("save row name",
        () => saveRowPresenterType.Value?.Methods().Instance().Returns(typeof(void)).Takes(typeof(string), typeof(string)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("ShowName");
}
