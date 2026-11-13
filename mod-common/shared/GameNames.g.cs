// Generated for the game 0.82.09 (tools/game-names from the game's files, or the research plugin's `aliases`). Do not edit:
// make it again (docs/game-names.md). The lookups behind each name are the Lookup.Generate ones in Game*.cs,
// the choices among several candidates are in GameNames.pins.txt. A name is public when it shows no obfuscated class
// (GameTypes.g.cs): it then stays the same on the next version of the game; the others are the library's own.

namespace Nobirds.OldenEra.Modding.Common;

public static class GameNames
{
    public const string GameVersion = "0.82.09";

    // town building slot: name field
    public const string BuildingSlotNameField = "<clsx>k__BackingField";

    // weak squad share
    public const float WeakShare = 0.5f;

    // The values the getters give, those that are there (a list name: Lookup.GenerateAll).
    static global::System.Collections.Generic.IEnumerable<T> Each<T>(params global::System.Func<T>[] getters) where T : class
    {
        foreach (var getter in getters)
        {
            T value = null;
            try { value = getter(); }
            catch (global::System.Exception) { }
            if (value != null)
                yield return value;
        }
    }

    // always battle (static): lh.bvek : Boolean
    public static bool AlwaysBattle => global::lh.bvek;

    // forecast builder: exw.bjft(DamageUiParams, Boolean) : exv
    internal static GameForecast BuildForecast(global::Hex.Session.Battle.DamageUiParams a0, bool a1) => global::exw.bjft(a0, a1);

    // data roots: bkp.byyc : Il2CppReferenceArray`1
    internal static global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameDataRoot> DataRoots { get => global::bkp.byyc; set => global::bkp.byyc = value; }

    // encounter: dkw.zku(fgd, frc) : Void
    internal static void Encounter(GameHero a0, GameSquad a1) => global::dkw.zku(a0, a1);

    // HUD hero panel: BhHeroHUDPanel.me : BhHeroHUDPanel
    public static global::Hex.Session.Hud.BhHeroHUDPanel HudPanel => global::Hex.Session.Hud.BhHeroHUDPanel.me;

    // diplomacy check: ffd.blpa(fgd, frc) : Boolean
    internal static bool JoinsByDiplomacy(GameHero a0, GameSquad a1) => global::ffd.blpa(a0, a1);

    // diplomacy check explained: ffd.bloz(fgd, frc, String&) : Boolean
    internal static bool JoinsByDiplomacyExplained(GameHero a0, GameSquad a1, out string a2) => global::ffd.bloz(a0, a1, out a2);

    // join share: ffd.blpb(frc, fgd) : Single
    internal static float JoinShare(GameSquad a0, GameHero a1) => global::ffd.blpb(a0, a1);

    // localization service: iv.buwm : cav
    internal static GameLocalization Localization => global::iv.buwm;

    // map session: feh.me : feh
    internal static GameMapSession MapSession => global::feh.me;

    // resource manager instance: ResManager.bsci() : ResManager
    public static global::Hex.ResManager ResourceManager() => global::Hex.ResManager.bsci();

    // screen registry instance: cqp.ujo() : cqp
    internal static GameScreenRegistry ScreenRegistry() => global::cqp.ujo();

    // session: dkd.Me : dkd
    internal static GameSession Session => global::dkd.Me;

    // settings service: bak.bwst : bak
    internal static GameSettingsService SettingsService => global::bak.bwst;

    // sprite registry instance: coi.cdxf : coi
    internal static GameSpriteRegistry SpriteRegistry => global::coi.cdxf;

    // tooltips holder: BhTooltips.cftw : BhTooltips
    public static global::Hex.UI.Tooltips.BhTooltips Tooltips => global::Hex.UI.Tooltips.BhTooltips.cftw;

    // unit portraits: efr.bdbl(String) : Sprite
    public static global::UnityEngine.Sprite UnitPortrait(string a0) => global::efr.bdbl(a0);

    extension(GameAbilities abilities)
    {
        // ability container -> ability (EAbilityType): ejh.betm(EAbilityType) : eji
        internal GameAbility Ability(global::Hex.Configs.EAbilityType a0) => abilities.betm(a0);
    }

    extension(GameAbility ability)
    {
        // ability -> buff configs: eji.clzc.cvnb, eji.clzc.cvnc, eji.clzc.cvnd
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffConfig> BuffConfigs => GameNames.Each<global::Hex.Configs.BuffConfig>(() => ability.clzc?.cvnb, () => ability.clzc?.cvnc, () => ability.clzc?.cvnd);

        // ability -> cast configs: eji.bevf().config, eji.bevf().data, eji.bevg().config, eji.bevg().data, eji.clyy.config, eji.clyy.data, eji.clyz.config, eji.clyz.data, eji.clza.config, eji.clza.data, eji.clzc.damageDealer, eji.clzc.selfDealer, eji.clzc.allyDealer
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Configs.CastDealerConfig> CastConfigs => GameNames.Each<global::Hex.Configs.CastDealerConfig>(() => ability.bevf()?.config, () => ability.bevf()?.data, () => ability.bevg()?.config, () => ability.bevg()?.data, () => ability.clyy?.config, () => ability.clyy?.data, () => ability.clyz?.config, () => ability.clyz?.data, () => ability.clza?.config, () => ability.clza?.data, () => ability.clzc?.damageDealer, () => ability.clzc?.selfDealer, () => ability.clzc?.allyDealer);

        // ability -> AbilityLogicConfig: eji.clzc : AbilityLogicConfig
        internal global::Hex.Configs.AbilityLogicConfig LogicConfig => ability.clzc;
    }

    extension(GameBattleLogic battleLogic)
    {
        // battle logic: the defender answers before the strike: eqe.bhqj(Unit) : Boolean
        internal bool AnswersBeforeStrike(global::Hex.Session.Battle.Unit a0) => battleLogic.bhqj(a0);

        // battle logic: a second strike after the counter: eqe.bhqk(Unit, eji) : Boolean
        internal bool StrikesAgainAfterCounter(global::Hex.Session.Battle.Unit a0, GameAbility a1) => battleLogic.bhqk(a0, a1);
    }

    extension(GameBonus bonus)
    {
        // bonus -> receiver: dwa.cjfj : dwc
        internal GameBonusReceiver Receiver => bonus.cjfj;
    }

    extension(GameBonusReceiver bonusReceiver)
    {
        // bonus receiver add: dwc.bavu(dwa, Boolean) : Void
        internal void Add(GameBonus a0, bool a1) => bonusReceiver.bavu(a0, a1);
    }

    extension(GameBuffs buffs)
    {
        // buffs -> lists: elg.cmgs, elg.cmgt, elg.cmgv
        internal global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.List<GameBuff>> Lists => GameNames.Each<global::Il2CppSystem.Collections.Generic.List<GameBuff>>(() => buffs.cmgs, () => buffs.cmgt, () => buffs.cmgv);
    }

    extension(GameCityScenes cityScenes)
    {
        // town scenes: registry: eja.clvl : cht
        internal GameSceneRegistry Registry => cityScenes.clvl;
    }

    extension(GameDataRoot dataRoot)
    {
        // data root enabled: bko.byxz : Boolean
        internal bool Enabled { get => dataRoot.byxz; set => dataRoot.byxz = value; }

        // data root folder: bko.byyb : String
        internal string Folder { get => dataRoot.byyb; set => dataRoot.byyb = value; }

        // data root name: bko.byya : String
        internal string Name { get => dataRoot.byya; set => dataRoot.byya = value; }
    }

    extension(GameDwelling dwelling)
    {
        // dwelling -> bonuses: fpq.crjc : List`1
        internal global::Il2CppSystem.Collections.Generic.List<GameBonus> Bonuses => dwelling.crjc;
    }

    extension(GameEntity entity)
    {
        // meeting check: fhf.bmed(fhf) : Boolean
        internal bool Meets(GameEntity a0) => entity.bmed(a0);
    }

    extension(GameForecast forecast)
    {
        // forecast: damage max: exv.cpap : Int32
        internal int DamageMax { get => forecast.cpap; set => forecast.cpap = value; }

        // forecast: damage min: exv.cpao : Int32
        internal int DamageMin { get => forecast.cpao; set => forecast.cpao = value; }

        // forecast: killed max: exv.cpau : Int32
        internal int KilledMax { get => forecast.cpau; set => forecast.cpau = value; }

        // forecast: killed min: exv.cpat : Int32
        internal int KilledMin { get => forecast.cpat; set => forecast.cpat = value; }
    }

    extension(GameHero hero)
    {
        // hero strength: fgd.blwn(Boolean) : Int32
        internal int ArmyStrength(bool a0) => hero.blwn(a0);

        // hero -> its data (Hex.Session.Data.Hero, with the party): fgd.cwuf : Hero
        internal global::Hex.Session.Data.Hero Data => hero.cwuf;

        // hero -> entity: fgd.cqfl : fhf
        internal GameEntity Entity => hero.cqfl;

        // hero -> HeroStat paths: fgd.cqfc.cjrl, fgd.cqfc.cjrm, fgd.cwuf.statsByLevel, fgd.cwuf.additionalStats, fgd.cwug, fgd.cwuh
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Session.Data.HeroStat> HeroStats => GameNames.Each<global::Hex.Session.Data.HeroStat>(() => hero.cqfc?.cjrl, () => hero.cqfc?.cjrm, () => hero.cwuf?.statsByLevel, () => hero.cwuf?.additionalStats, () => hero.cwug, () => hero.cwuh);

        // hero -> Logic.Hero: fgd.cqfc : Hero
        internal global::Hex.Session.Logic.Hero Logic => hero.cqfc;

        // hero -> side wrapper: fgd.cqfd : fqu
        internal GameSide SideOwner => hero.cqfd;
    }

    extension(GameInputBoxOptions inputBoxOptions)
    {
        // input box cancel text: dck.chgt : String
        internal string CancelText { get => inputBoxOptions.chgt; set => inputBoxOptions.chgt = value; }

        // input box caption: dck.chgq : String
        internal string Caption { get => inputBoxOptions.chgq; set => inputBoxOptions.chgq = value; }

        // input box confirm text: dck.chgs : String
        internal string ConfirmText { get => inputBoxOptions.chgs; set => inputBoxOptions.chgs = value; }

        // input box on cancel: dck.chgy : Action
        internal global::Il2CppSystem.Action OnCancel { get => inputBoxOptions.chgy; set => inputBoxOptions.chgy = value; }

        // input box on confirm: dck.chgz : Action`1
        internal global::Il2CppSystem.Action<string> OnConfirm { get => inputBoxOptions.chgz; set => inputBoxOptions.chgz = value; }

        // input box placeholder: dck.chgr : String
        internal string Placeholder { get => inputBoxOptions.chgr; set => inputBoxOptions.chgr = value; }
    }

    extension(GameInputReceiver inputReceiver)
    {
        // bind key: cbe.qpu(EInputAction, EInputState, Action) : Void
        internal void Bind(global::Hex.InputSys.EInputAction a0, global::Hex.InputSys.EInputState a1, global::Il2CppSystem.Action a2) => inputReceiver.qpu(a0, a1, a2);
    }

    extension(GameIntroStep introStep)
    {
        // intro videos step done: ccj.ccgr : Boolean
        internal bool Done { get => introStep.ccgr; set => introStep.ccgr = value; }
    }

    extension(GameLocalization localization)
    {
        // text of a key: cav.qmq(String) : String
        internal string Translate(string a0) => localization.qmq(a0);
    }

    extension(GameMapGameSession mapGameSession)
    {
        // session -> map data: dcz.chmz : Data
        internal global::Hex.Session.Data.Data MapData => mapGameSession.chmz;

        // map session: its scenes: dcz.chnb : dkh
        internal GameSessionScenes Scenes => mapGameSession.chnb;
    }

    extension(GameMapObject mapObject)
    {
        // map object -> model: fql.crli : bye
        internal GameModelHolder Model => mapObject.crli;

        // map object -> DataObject: fql.crlj : DataObject
        internal global::Hex.Session.Data.DataObject ObjectData => mapObject.crlj;

        // map object -> owner side: fql.crll : fqu
        internal GameSide Owner => mapObject.crll;

        // map object -> party: fql.crlk : eco
        internal GameParty Party => mapObject.crlk;

        // map object visited by side: fql.bnpm(fqu) : Boolean
        internal bool VisitedBy(GameSide a0) => mapObject.bnpm(a0);
    }

    extension(GameMapObjectStore mapObjectStore)
    {
        // map objects by id: fqm.crlz : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<int, GameMapObject> ById => mapObjectStore.crlz;
    }

    extension(GameMapSession mapSession)
    {
        // map session -> map objects: feh.cpyx : fqm
        internal GameMapObjectStore MapObjects => mapSession.cpyx;

        // map session -> squads: feh.cpyz : fre
        internal GameSquadStore Squads => mapSession.cpyz;
    }

    extension(GameMessageBoxButton messageBoxButton)
    {
        // message box button click: dcm.chhn : Action
        internal global::Il2CppSystem.Action Click { get => messageBoxButton.chhn; set => messageBoxButton.chhn = value; }

        // message box button label: dcm.chhm : String
        internal string Label { get => messageBoxButton.chhm; set => messageBoxButton.chhm = value; }
    }

    extension(GameMessageBoxOptions messageBoxOptions)
    {
        // message box cancel button: dcl.chhh : dcm
        internal GameMessageBoxButton Cancel { get => messageBoxOptions.chhh; set => messageBoxOptions.chhh = value; }

        // message box caption: dcl.chhc : String
        internal string Caption { get => messageBoxOptions.chhc; set => messageBoxOptions.chhc = value; }

        // message box confirm button: dcl.chhg : dcm
        internal GameMessageBoxButton Confirm { get => messageBoxOptions.chhg; set => messageBoxOptions.chhg = value; }

        // message box text: dcl.chhd : String
        internal string Text { get => messageBoxOptions.chhd; set => messageBoxOptions.chhd = value; }
    }

    extension(GameModelHolder modelHolder)
    {
        // map object model -> GameObjects: bye._cbpg_k__BackingField, bye.cbpj, bye.cbpg
        internal global::System.Collections.Generic.IEnumerable<global::UnityEngine.GameObject> SceneObjects => GameNames.Each<global::UnityEngine.GameObject>(() => modelHolder._cbpg_k__BackingField, () => modelHolder.cbpj, () => modelHolder.cbpg);
    }

    extension(GameResourceEntry resourceEntry)
    {
        // resource entry: asset: gpu.ctza : Object
        internal global::UnityEngine.Object Asset { get => resourceEntry.ctza; set => resourceEntry.ctza = value; }

        // resource entry: key: gpu.ctyz : gpt
        internal GameResourceKey Key { get => resourceEntry.ctyz; set => resourceEntry.ctyz = value; }
    }

    extension(GameResourceKey resourceKey)
    {
        // resource key: type: gpt.ctyy : Type
        internal global::Il2CppSystem.Type AssetType { get => resourceKey.ctyy; set => resourceKey.ctyy = value; }

        // resource key: bundle: gpt.ctyx : String
        internal string Bundle { get => resourceKey.ctyx; set => resourceKey.ctyx = value; }

        // resource key: path: gpt.ctyw : String
        internal string Path { get => resourceKey.ctyw; set => resourceKey.ctyw = value; }
    }

    extension(GameSaveRowPresenter saveRowPresenter)
    {
        // save row on click: cum.kmh(Action) : Void
        internal void OnClick(global::Il2CppSystem.Action a0) => saveRowPresenter.kmh(a0);

        // save row name: cum.cqm(String, String) : Void
        internal void ShowName(string a0, string a1) => saveRowPresenter.cqm(a0, a1);
    }

    extension(GameSceneEntry sceneEntry)
    {
        // scene entry: roots: chs.ccmm : List`1
        internal global::Il2CppSystem.Collections.Generic.List<global::UnityEngine.GameObject> Roots { get => sceneEntry.ccmm; set => sceneEntry.ccmm = value; }

        // scene entry: EScene: chs.ccmj : EScene
        internal global::Hex.GameScenes.EScene SceneId { get => sceneEntry.ccmj; set => sceneEntry.ccmj = value; }

        // scene entry: take the roots: chs.rba() : Void
        internal void TakeRoots() => sceneEntry.rba();

        // scene entry: scene: chs.ccmk : Scene
        internal global::UnityEngine.SceneManagement.Scene UnityScene { get => sceneEntry.ccmk; set => sceneEntry.ccmk = value; }
    }

    extension(GameSceneRegistry sceneRegistry)
    {
        // scene registry: add: cht.rbb(Il2CppReferenceArray`1) : Void
        internal void AddScenes(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameSceneEntry> a0) => sceneRegistry.rbb(a0);

        // scene registry: scenes: cht.ccmn : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<global::Hex.GameScenes.EScene, GameSceneEntry> Scenes => sceneRegistry.ccmn;

        // scene registry: turn a scene on or off: cht.rbf(EScene, Boolean) : Void
        internal void SetActive(global::Hex.GameScenes.EScene a0, bool a1) => sceneRegistry.rbf(a0, a1);
    }

    extension(GameScreenRegistry screenRegistry)
    {
        // hide screen by name: cqp.jbp(String) : Void
        internal void Hide(string a0) => screenRegistry.jbp(a0);

        // open screen by name: cqp.hly(String) : BhScreen
        internal global::Hex.UI.BhScreen Open(string a0) => screenRegistry.hly(a0);

        // open screen: cqp.ujv(BhScreen) : BhScreen
        internal global::Hex.UI.BhScreen OpenScreen(global::Hex.UI.BhScreen a0) => screenRegistry.ujv(a0);

        // open screens: cqp.cepp : List`1
        internal global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.BhScreen> OpenScreens => screenRegistry.cepp;

        // register screen: cqp.ujr(BhScreen) : Void
        internal void Register(global::Hex.UI.BhScreen a0) => screenRegistry.ujr(a0);

        // screen by name: cqp.ujs(String) : BhScreen
        internal global::Hex.UI.BhScreen Screen(string a0) => screenRegistry.ujs(a0);

        // registered screens: cqp.cepo : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<string, global::Hex.UI.BhScreen> Screens => screenRegistry.cepo;
    }

    extension(GameSession session)
    {
        // session -> how it is played (EPlay): dkd.cwcc : EPlay
        internal global::Hex.EPlay Play => session.cwcc;
    }

    extension(GameSessionScenes sessionScenes)
    {
        // session scenes: town: dkh.cijb : eja
        internal GameCityScenes City => sessionScenes.cijb;
    }

    extension(GameSettingsService settingsService)
    {
        // settings: bak.bwsu : SettingsData
        internal global::Hex.Settings.Data.SettingsData Settings => settingsService.bwsu;
    }

    extension(GameSide side)
    {
        // side -> AI: fqu.crnn : fzh
        internal GameSideAi Ai => side.crnn;

        // side is a person's: fqu.bnyz() : Boolean
        internal bool IsPerson() => side.cwyx;

        // side -> logic with the bonus receiver: fqu.crms : edc
        internal GameSideLogic Logic => side.crms;

        // side wrapper -> Side: fqu.cwyf : Side
        internal global::Hex.Session.Data.Side Side => side.cwyf;

        // side -> state: fqu.crnb : fmr
        internal GameSideState State => side.crnb;
    }

    extension(GameSideLogic sideLogic)
    {
        // side logic -> bonus receiver: edc.cjzi : dyj
        internal GameSideBonusReceiver BonusReceiver => sideLogic.cjzi;
    }

    extension(GameSideState sideState)
    {
        // side state: no flee offers: fmr.cqzb : Boolean
        internal bool NoFleeOffers => sideState.cqzb;
    }

    extension(GameSimpleTabData simpleTabData)
    {
        // simple tab click: dcs.chjp : Action
        internal global::Il2CppSystem.Action Click { get => simpleTabData.chjp; set => simpleTabData.chjp = value; }

        // simple tab label: dcs.chjo : String
        internal string Label { get => simpleTabData.chjo; set => simpleTabData.chjo = value; }
    }

    extension(GameSpriteRegistry spriteRegistry)
    {
        // sprite tables: coi.cdxg, coi.cdxh, coi.cdxi, coi.cdxj, coi.cdxk, coi.cdxl, coi.cdxm, coi.cdxn, coi.cdxo, coi.cdxp, coi.cdxq, coi.cdxr, coi.cdxs, coi.cdxt, coi.cdxu, coi.cdxv, coi.cdxw, coi.cdxx, coi.cdxy, coi.cdxz, coi.cdya, coi.cdyb, coi.cdyc, coi.cdyd, coi.cdye, coi.cdyf, coi.cdyg, coi.cdyh, coi.cdyi, coi.cdyj, coi.cdyk, coi.cdyl, coi.cdym, coi.cdyn, coi.cdyo, coi.cdyp, coi.cdyq, coi.cdyr, coi.cdys, coi.cdyt, coi.cdyu
        internal global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>> Tables => GameNames.Each<global::Il2CppSystem.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>>(() => spriteRegistry.cdxg, () => spriteRegistry.cdxh, () => spriteRegistry.cdxi, () => spriteRegistry.cdxj, () => spriteRegistry.cdxk, () => spriteRegistry.cdxl, () => spriteRegistry.cdxm, () => spriteRegistry.cdxn, () => spriteRegistry.cdxo, () => spriteRegistry.cdxp, () => spriteRegistry.cdxq, () => spriteRegistry.cdxr, () => spriteRegistry.cdxs, () => spriteRegistry.cdxt, () => spriteRegistry.cdxu, () => spriteRegistry.cdxv, () => spriteRegistry.cdxw, () => spriteRegistry.cdxx, () => spriteRegistry.cdxy, () => spriteRegistry.cdxz, () => spriteRegistry.cdya, () => spriteRegistry.cdyb, () => spriteRegistry.cdyc, () => spriteRegistry.cdyd, () => spriteRegistry.cdye, () => spriteRegistry.cdyf, () => spriteRegistry.cdyg, () => spriteRegistry.cdyh, () => spriteRegistry.cdyi, () => spriteRegistry.cdyj, () => spriteRegistry.cdyk, () => spriteRegistry.cdyl, () => spriteRegistry.cdym, () => spriteRegistry.cdyn, () => spriteRegistry.cdyo, () => spriteRegistry.cdyp, () => spriteRegistry.cdyq, () => spriteRegistry.cdyr, () => spriteRegistry.cdys, () => spriteRegistry.cdyt, () => spriteRegistry.cdyu);
    }

    extension(GameSquad squad)
    {
        // squad -> entity: frc.crpg : fhf
        internal GameEntity Entity => squad.crpg;

        // squad -> DataSquad: frc.crov : DataSquad
        internal global::Hex.Session.Data.DataSquad SquadData => squad.crov;

        // squad -> hero-like owners: frc.crpk, frc.cwzd
        internal global::System.Collections.Generic.IEnumerable<GameHero> SquadOwners => GameNames.Each<GameHero>(() => squad.crpk, () => squad.cwzd);

        // squad strength: frc.boeu() : Int32
        internal int SquadStrength() => squad.boeu();

        // squad -> view: frc.crpb : fno
        internal GameSquadView View => squad.crpb;
    }

    extension(GameSquadEntity squadEntity)
    {
        // squad entity -> squad: fgy.cqhy : frc
        internal GameSquad Squad => squadEntity.cqhy;
    }

    extension(GameSquadStore squadStore)
    {
        // squads by id: fre.crpr : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<int, GameSquad> ById => squadStore.crpr;
    }

    extension(GameSquadView squadView)
    {
        // squad view -> GameObject: fno.crdd : GameObject
        internal global::UnityEngine.GameObject Model => squadView.crdd;
    }

    extension(GameUnitBase unitBase)
    {
        // Unit -> ability container: eon.bgya() : ejh
        internal GameAbilities Abilities() => unitBase.cndf;

        // Unit -> buffs: eon.cwop : elg
        internal GameBuffs Buffs => unitBase.cwop;
    }

    extension(global::Hex.Configs.UnitPassiveConfig it)
    {
        // passive -> buff data: UnitPassiveConfig.data
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffData> BuffDatas => GameNames.Each<global::Hex.Configs.BuffData>(() => it.data);
    }

    extension(global::Hex.GameHub.UICommon.BhTab it)
    {
        // hub tab click: BhTab.ccmx : Action`1
        internal global::Il2CppSystem.Action<GameTabItem> Click { get => it.ccmx; set => it.ccmx = value; }

        // hub tab available: BhTab.jiu(Boolean) : Void
        public void SetAvailable(bool a0) => it.jiu(a0);

        // hub tab selected look: BhTab.jiv(Boolean) : Void
        public void ShowSelected(bool a0) => it.jiv(a0);
    }

    extension(global::Hex.MainMenu.MainMenuNavigationView it)
    {
        // main menu open sub-menu: MainMenuNavigationView.ccch : MainMenuSubNavView
        public global::Hex.MainMenu.MainMenuSubNavView OpenSubMenu => it.ccch;

        // main menu select: MainMenuNavigationView.qjv(cab) : Boolean
        internal bool Select(GameMenuSelectable a0) => it.qjv(a0);
    }

    extension(global::Hex.ResManager it)
    {
        // resource table: ResManager.ctzm : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<GameResourceKey, GameResourceEntry> Assets => it.ctzm;
    }

    extension(global::Hex.Session.Battle.Unit it)
    {
        // Unit -> UnitData: Unit.cphq : UnitData
        public global::Hex.Session.Battle.UnitData Data => it.cphq;

        // unit: has a sequence effect: Unit.bjoj(String) : Boolean
        public bool HasSequenceEffect(string a0) => it.bjoj(a0);

        // Unit -> UnitLogicConfig: Unit.cpho : UnitLogicConfig
        public global::Hex.Configs.UnitLogicConfig LogicConfig => it.cpho;
    }

    extension(global::Hex.Session.Battle.UnitData it)
    {
        // UnitData: top creature's health: UnitData.cpmy : Int32
        public int TopHealth { get => it.cpmy; set => it.cpmy = value; }
    }

    extension(global::Hex.Session.Data.DataFogOfWar it)
    {
        // fog: cell hidden: DataFogOfWar.bkix(Int32) : Boolean
        public bool InFog(int a0) => it.bkix(a0);
    }

    extension(global::Hex.Session.Data.DataObject it)
    {
        // map object data -> configs: DataObject.cpvo, DataObject.cwtj
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.ObjConfigBase> Configs => GameNames.Each<global::Hex.Configs.ObjConfigBase>(() => it.cpvo, () => it.cwtj);
    }

    extension(global::Hex.Session.Data.Hero it)
    {
        // party wrapper -> DataParty: Hero.party : DataParty
        public global::Hex.Session.Data.DataParty Army => it.party;
    }

    extension(global::Hex.Session.Hud.BhHeroHUDPanel it)
    {
        // HUD hero panel -> hero: BhHeroHUDPanel.cill : fgd
        internal GameHero Hero => it.cill;
    }

    extension(global::Hex.Session.Logic.Hero it)
    {
        // hero visit id: Hero.cwfu : Int32
        public int VisitId => it.cwfu;
    }

    extension(global::Hex.Settings.UI.BhSettingsCheckbox it)
    {
        // switch callback: BhSettingsCheckbox.bwrf : Action`1
        public global::Il2CppSystem.Action<bool> SwitchCallback { get => it.bwrf; set => it.bwrf = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsSection it)
    {
        // section type: BhSettingsSection.bwrj : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionType => it.bwrj;

        // section type field: BhSettingsSection._bwrj_k__BackingField : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionTypeStored { get => it._bwrj_k__BackingField; set => it._bwrj_k__BackingField = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsTab it)
    {
        // tab click action: BhSettingsTab.bwrp : Action`1
        public global::Il2CppSystem.Action<global::Hex.Settings.UI.ESettingsType> ClickAction { get => it.bwrp; set => it.bwrp = value; }

        // tab selected look: BhSettingsTab.kqf(Boolean) : Void
        public void ShowSelected(bool a0) => it.kqf(a0);
    }

    extension(global::Hex.Settings.UI.SettingsSectionsView it)
    {
        // shown section: SettingsSectionsView.bwsl : BhSettingsSection
        public global::Hex.Settings.UI.BhSettingsSection ShownSection => it.bwsl;

        // settings show section: SettingsSectionsView.ktn(ESettingsType) : Void
        public void ShowSection(global::Hex.Settings.UI.ESettingsType a0) => it.ktn(a0);
    }

    extension(global::Hex.Storage.IconData it)
    {
        // PNG to sprite: IconData.jvm() : Sprite
        public global::UnityEngine.Sprite ToSprite() => it.jvm();
    }

    extension(global::Hex.UI.BhGenericButton it)
    {
        // button available: BhGenericButton.uhi(Boolean) : Void
        public void SetAvailable(bool a0) => it.uhi(a0);
    }

    extension(global::Hex.UI.BhScreen it)
    {
        // screen hidden at once: BhScreen.udt() : Void
        public void HideAtOnce() => it.udt();

        // screen input: BhScreen.celi : cbe
        internal GameInputReceiver Input => it.celi;

        // screen is open: BhScreen.celf : Boolean
        public bool IsOpen => it.celf;

        // screen one-time init: BhScreen.hbq() : Void
        public void SetUpOnce() => it.hbq();

        // screen when closed: BhScreen.cela : Action
        public global::Il2CppSystem.Action WhenClosed { get => it.cela; set => it.cela = value; }
    }

    extension(global::Hex.UI.BhSkulls it)
    {
        // BhSkulls level colour: BhSkulls.umy(Int32) : Color
        public global::UnityEngine.Color DangerColor(int a0) => it.umy(a0);

        // BhSkulls level (hero power, squad power): BhSkulls.umx(Int32, Int32) : Int32
        public int DangerLevel(int a0, int a1) => it.umx(a0, a1);

        // BhSkulls fill for a party: BhSkulls.umw(DataParty) : Int32
        public int ShowDangerOfParty(global::Hex.Session.Data.DataParty a0) => it.umw(a0);

        // BhSkulls fill for a squad: BhSkulls.umv(frc) : Int32
        internal int ShowDangerOfSquad(GameSquad a0) => it.umv(a0);
    }

    extension(global::Hex.UI.BhTooltipGarnison it)
    {
        // BhTooltipGarnison fill (party, bool): BhTooltipGarnison.unr(eco, Boolean) : Void
        internal void FillWith(GameParty a0, bool a1) => it.unr(a0, a1);
    }

    extension(global::Hex.UI.GeneralKit.BhCheckbox it)
    {
        // switch is on: BhCheckbox.xzj() : Boolean
        public bool SwitchedOn() => it.xzj();
    }

    extension(global::Hex.UI.GeneralKit.BhNavigationButton it)
    {
        // menu button click: BhNavigationButton.chcb : Action
        public global::Il2CppSystem.Action MenuClick { get => it.chcb; set => it.chcb = value; }
    }

    extension(global::Hex.UI.GeneralKit.BhTag it)
    {
        // tag chip colour: BhTag.yfb(Color) : Void
        public void ShowColor(global::UnityEngine.Color a0) => it.yfb(a0);

        // tag chip text: BhTag.yez(String) : Void
        public void ShowText(string a0) => it.yez(a0);
    }

    extension(global::Hex.UI.GeneralKit.BhToggle it)
    {
        // check mark show value: BhToggle.yfd(Boolean) : Void
        public void ShowValue(bool a0) => it.yfd(a0);
    }

    extension(global::Hex.UI.SaveLoad.BhFilter it)
    {
        // save filter elements: BhFilter.cfbm : List`1
        public global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.SaveLoad.BhFilterElement> Elements => it.cfbm;

        // save filter set up: BhFilter.uxq(Il2CppStringArray, Action) : Void
        public void SetUp(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray a0, global::Il2CppSystem.Action a1) => it.uxq(a0, a1);
    }

    extension(global::Hex.UI.SaveLoad.BhFilterElement it)
    {
        // save filter element ticked: BhFilterElement.uxw() : Boolean
        public bool Ticked() => it.uxw();
    }

    extension(global::Hex.UI.SaveLoad.BhSessionInfoElement it)
    {
        // save row presenter: BhSessionInfoElement.vfk() : cum
        internal GameSaveRowPresenter Presenter() => it.vfk();

        // save row campaign shown: BhSessionInfoElement.vfs(Boolean) : Void
        public void ShowCampaign(bool a0) => it.vfs(a0);

        // save row icon: BhSessionInfoElement.vfn(Sprite) : Void
        public void ShowIcon(global::UnityEngine.Sprite a0) => it.vfn(a0);

        // save row cloud: BhSessionInfoElement.vfm(Boolean) : Void
        public void ShowInCloud(bool a0) => it.vfm(a0);

        // save row map icons shown: BhSessionInfoElement.vfv(Boolean) : Void
        public void ShowMapIcons(bool a0) => it.vfv(a0);

        // save row selected look: BhSessionInfoElement.vfy(Boolean) : Void
        public void ShowSelected(bool a0) => it.vfy(a0);

        // save row tag: BhSessionInfoElement.vfp(Int32, String, Color) : Void
        public void ShowTag(int a0, string a1, global::UnityEngine.Color a2) => it.vfp(a0, a1, a2);

        // save row tag shown: BhSessionInfoElement.vfq(Int32, Boolean) : Void
        public void ShowTagShown(int a0, bool a1) => it.vfq(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltip it)
    {
        // tooltip -> positioner: BhTooltip.cfsu : cxg
        internal GameTooltipPositioner Positioner => it.cfsu;

        // tooltip set texts: BhTooltip.vzx(BasicTooltip, crv) : Void
        internal void SetTexts(global::Hex.UI.BasicTooltip a0, GameTooltipOptions a1) => it.vzx(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipHire it)
    {
        // BhTooltipHire fill (map object): BhTooltipHire.wam(fpq) : Void
        internal void FillFor(GameDwelling a0) => it.wam(a0);
    }

    extension(global::Hex.UI.Tooltips.BhTooltips it)
    {
        // text tooltip: BhTooltips.wcd(String, String, String, RectTransform, crv, ESnapDirection, Boolean) : Void
        internal void ShowTexts(string a0, string a1, string a2, global::UnityEngine.RectTransform a3, GameTooltipOptions a4, global::Hex.UI.Tooltips.ESnapDirection a5, bool a6) => it.wcd(a0, a1, a2, a3, a4, a5, a6);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipSquad it)
    {
        // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.wes(ESquadTooltipType, Object) : Void
        public void FillFor(global::Hex.UI.Tooltips.ESquadTooltipType a0, global::Il2CppSystem.Object a1) => it.wes(a0, a1);
    }
}

// The game's methods the core and the mods patch, as the exact members of the game's classes (no search while the
// game runs); found by the lookups in search/Game*.cs marked GenerateTarget.
internal static class GameTargets
{
    const global::System.Reflection.BindingFlags Any = global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic |
        global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.DeclaredOnly;

    static global::System.Reflection.MethodInfo M(global::System.Type type, string name, params global::System.Type[] parameters) =>
        type.GetMethod(name, Any, null, parameters, null);

    // town scenes off but the kept: eja.bepf(HashSet`1) : Void, eja.ksd(HashSet`1) : Void, eja.oks(HashSet`1) : Void, eja.dca(HashSet`1) : Void
    public static readonly global::System.Reflection.MethodInfo[] CityScenesOff =
    {
        M(typeof(GameCityScenes), nameof(GameCityScenes.bepf), typeof(global::Il2CppSystem.Collections.Generic.HashSet<global::Hex.GameScenes.EScene>)),
        M(typeof(GameCityScenes), nameof(GameCityScenes.ksd), typeof(global::Il2CppSystem.Collections.Generic.HashSet<global::Hex.GameScenes.EScene>)),
        M(typeof(GameCityScenes), nameof(GameCityScenes.oks), typeof(global::Il2CppSystem.Collections.Generic.HashSet<global::Hex.GameScenes.EScene>)),
        M(typeof(GameCityScenes), nameof(GameCityScenes.dca), typeof(global::Il2CppSystem.Collections.Generic.HashSet<global::Hex.GameScenes.EScene>)),
    };

    // counter decision: eqe.bhqt(Unit, Unit, eji, Hex, List`1) : Boolean
    public static readonly global::System.Reflection.MethodInfo CounterDecision = M(typeof(GameBattleLogic), nameof(GameBattleLogic.bhqt), typeof(global::Hex.Session.Battle.Unit), typeof(global::Hex.Session.Battle.Unit), typeof(GameAbility), typeof(global::Hex.Session.Battle.Hex), typeof(global::Il2CppSystem.Collections.Generic.List<global::Hex.Session.Battle.Hex>));

    // data roots builders: bkp.cvc() : Void, bkp.nby() : Void
    public static readonly global::System.Reflection.MethodInfo[] DataRootsBuilders =
    {
        M(typeof(global::bkp), nameof(global::bkp.cvc)),
        M(typeof(global::bkp), nameof(global::bkp.nby)),
    };

    // diplomacy points (squad, hero): ffd.blpe(fgd, frc, DiplomacyInfoConfig) : Int32, ffd.blpg(fgd, frc, DiplomacyInfoConfig) : Int32
    public static readonly global::System.Reflection.MethodInfo[] DiplomacyPoints =
    {
        M(typeof(global::ffd), nameof(global::ffd.blpe), typeof(GameHero), typeof(GameSquad), typeof(global::Hex.Configs.DiplomacyInfoConfig)),
        M(typeof(global::ffd), nameof(global::ffd.blpg), typeof(GameHero), typeof(GameSquad), typeof(global::Hex.Configs.DiplomacyInfoConfig)),
    };

    // encounter: dkw.zku(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo Encounter = M(typeof(global::dkw), nameof(global::dkw.zku), typeof(GameHero), typeof(GameSquad));

    // encounter actions: fhu.bmfb(ELockType, fhf, fhf) : Void, efh.bcuz(frc, fgd) : Void, efh.bcvo(frc, fgd) : Void, dkw.zkt(frc, fgd) : Void, dkw.zkv(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo[] EncounterActions =
    {
        M(typeof(global::fhu), nameof(global::fhu.bmfb), typeof(global::Hex.Session.World.ELockType), typeof(GameEntity), typeof(GameEntity)),
        M(typeof(global::efh), nameof(global::efh.bcuz), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::efh), nameof(global::efh.bcvo), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zkt), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zkv), typeof(GameHero), typeof(GameSquad)),
    };

    // encounter battle: dkw.zkv(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterBattle = M(typeof(global::dkw), nameof(global::dkw.zkv), typeof(GameHero), typeof(GameSquad));

    // encounter: joins at once: dkw.zkt(frc, fgd) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterJoinAtOnce = M(typeof(global::dkw), nameof(global::dkw.zkt), typeof(GameSquad), typeof(GameHero));

    // forecast builders: exw.bjft(DamageUiParams, Boolean) : exv, exw.emy(DamageUiParams, Boolean) : exv, exw.igq(DamageUiParams, Boolean) : exv, exw.lsy(DamageUiParams, Boolean) : exv
    public static readonly global::System.Reflection.MethodInfo[] ForecastBuilders =
    {
        M(typeof(global::exw), nameof(global::exw.bjft), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.emy), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.igq), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.lsy), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
    };

    // BhTooltipHire fill (map object): BhTooltipHire.wam(fpq) : Void
    public static readonly global::System.Reflection.MethodInfo HireTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipHire), nameof(global::Hex.UI.Tooltips.BhTooltipHire.wam), typeof(GameDwelling));

    // intro videos step: ccj.qsd() : Void
    public static readonly global::System.Reflection.MethodInfo IntroVideos = M(typeof(GameIntroStep), nameof(GameIntroStep.qsd));

    // localized text: language changed: cpz.ihb() : Void
    public static readonly global::System.Reflection.MethodInfo LanguageChanged = M(typeof(GameLocalizedText), nameof(GameLocalizedText.ihb));

    // other forecast producers: none on this version
    public static readonly global::System.Reflection.MethodInfo[] OtherForecastMakers = global::System.Array.Empty<global::System.Reflection.MethodInfo>();

    // outline colors to the shader: bei.lku(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.osh(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void
    public static readonly global::System.Reflection.MethodInfo[] OutlineColorsToShader =
    {
        M(typeof(global::bei), nameof(global::bei.lku), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.osh), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
    };

    // screen registry init: cqp.Init() : Void
    public static readonly global::System.Reflection.MethodInfo ScreenRegistryInit = M(typeof(GameScreenRegistry), nameof(GameScreenRegistry.Init));

    // settings section fills: BhSettingsSection.Start() : Void, BhSettingsSection.Show(SettingsData) : Void, BhSettingsSection.kpm(SettingsData) : Void, BhSettingsSection.kpn(SettingsData) : Void
    public static readonly global::System.Reflection.MethodInfo[] SettingsSectionFills =
    {
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.Start)),
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.Show), typeof(global::Hex.Settings.Data.SettingsData)),
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.kpm), typeof(global::Hex.Settings.Data.SettingsData)),
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.kpn), typeof(global::Hex.Settings.Data.SettingsData)),
    };

    // settings show section: SettingsSectionsView.ktn(ESettingsType) : Void
    public static readonly global::System.Reflection.MethodInfo SettingsShowSection = M(typeof(global::Hex.Settings.UI.SettingsSectionsView), nameof(global::Hex.Settings.UI.SettingsSectionsView.ktn), typeof(global::Hex.Settings.UI.ESettingsType));

    // BhSkulls fill for a party: BhSkulls.umw(DataParty) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForParty = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umw), typeof(global::Hex.Session.Data.DataParty));

    // BhSkulls fill for a squad: BhSkulls.umv(frc) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForSquad = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umv), typeof(GameSquad));

    // BhSkulls level (hero power, squad power): BhSkulls.umx(Int32, Int32) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsLevel = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umx), typeof(int), typeof(int));

    // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.wes(ESquadTooltipType, Object) : Void
    public static readonly global::System.Reflection.MethodInfo SquadTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipSquad), nameof(global::Hex.UI.Tooltips.BhTooltipSquad.wes), typeof(global::Hex.UI.Tooltips.ESquadTooltipType), typeof(global::Il2CppSystem.Object));

    // positioner place for point: cxg.wea(Vector2) : Vector2, cwn.wea(Vector2) : Vector2, cwr.wea(Vector2) : Vector2
    public static readonly global::System.Reflection.MethodInfo[] TooltipPlaceForPoint =
    {
        M(typeof(GameTooltipPositioner), nameof(GameTooltipPositioner.wea), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwn), nameof(global::cwn.wea), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwr), nameof(global::cwr.wea), typeof(global::UnityEngine.Vector2)),
    };

    // tooltip set texts: BhTooltip.vzx(BasicTooltip, crv) : Void
    public static readonly global::System.Reflection.MethodInfo TooltipSetTexts = M(typeof(global::Hex.UI.Tooltips.BhTooltip), nameof(global::Hex.UI.Tooltips.BhTooltip.vzx), typeof(global::Hex.UI.BasicTooltip), typeof(GameTooltipOptions));
}
