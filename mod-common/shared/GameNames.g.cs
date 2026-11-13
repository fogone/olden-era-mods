// Generated for the game 0.82.10 (tools/game-names from the game's files, or the research plugin's `aliases`). Do not edit:
// make it again (docs/game-names.md). The lookups behind each name are the Lookup.Generate ones in Game*.cs,
// the choices among several candidates are in GameNames.pins.txt. A name is public when it shows no obfuscated class
// (GameTypes.g.cs): it then stays the same on the next version of the game; the others are the library's own.

namespace Nobirds.OldenEra.Modding.Common;

public static class GameNames
{
    public const string GameVersion = "0.82.10";

    // town building slot: name field
    public const string BuildingSlotNameField = "<clsk>k__BackingField";

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

    // always battle (static): lh.bvdx : Boolean
    public static bool AlwaysBattle => global::lh.bvdx;

    // forecast builder: exw.bjfi(DamageUiParams, Boolean) : exv
    internal static GameForecast BuildForecast(global::Hex.Session.Battle.DamageUiParams a0, bool a1) => global::exw.bjfi(a0, a1);

    // data roots: bkp.byxp : Il2CppReferenceArray`1
    internal static global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameDataRoot> DataRoots { get => global::bkp.byxp; set => global::bkp.byxp = value; }

    // encounter: dkw.zkj(fgd, frc) : Void
    internal static void Encounter(GameHero a0, GameSquad a1) => global::dkw.zkj(a0, a1);

    // HUD hero panel: BhHeroHUDPanel.me : BhHeroHUDPanel
    public static global::Hex.Session.Hud.BhHeroHUDPanel HudPanel => global::Hex.Session.Hud.BhHeroHUDPanel.me;

    // diplomacy check: ffd.blop(fgd, frc) : Boolean
    internal static bool JoinsByDiplomacy(GameHero a0, GameSquad a1) => global::ffd.blop(a0, a1);

    // diplomacy check explained: ffd.bloo(fgd, frc, String&) : Boolean
    internal static bool JoinsByDiplomacyExplained(GameHero a0, GameSquad a1, out string a2) => global::ffd.bloo(a0, a1, out a2);

    // join share: ffd.bloq(frc, fgd) : Single
    internal static float JoinShare(GameSquad a0, GameHero a1) => global::ffd.bloq(a0, a1);

    // localization service: iv.buvz : cav
    internal static GameLocalization Localization => global::iv.buvz;

    // map session: feh.me : feh
    internal static GameMapSession MapSession => global::feh.me;

    // resource manager instance: ResManager.bsbv() : ResManager
    public static global::Hex.ResManager ResourceManager() => global::Hex.ResManager.bsbv();

    // screen registry instance: cqp.uje() : cqp
    internal static GameScreenRegistry ScreenRegistry() => global::cqp.uje();

    // session: dkd.Me : dkd
    internal static GameSession Session => global::dkd.Me;

    // settings service: bak.bwsg : bak
    internal static GameSettingsService SettingsService => global::bak.bwsg;

    // sprite registry instance: coi.cdws : coi
    internal static GameSpriteRegistry SpriteRegistry => global::coi.cdws;

    // tooltips holder: BhTooltips.cftj : BhTooltips
    public static global::Hex.UI.Tooltips.BhTooltips Tooltips => global::Hex.UI.Tooltips.BhTooltips.cftj;

    // unit portraits: efr.bdba(String) : Sprite
    public static global::UnityEngine.Sprite UnitPortrait(string a0) => global::efr.bdba(a0);

    extension(GameAbilities abilities)
    {
        // ability container -> ability (EAbilityType): ejh.betb(EAbilityType) : eji
        internal GameAbility Ability(global::Hex.Configs.EAbilityType a0) => abilities.betb(a0);
    }

    extension(GameAbility ability)
    {
        // ability -> buff configs: eji.clyp.cvmo, eji.clyp.cvmp, eji.clyp.cvmq
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffConfig> BuffConfigs => GameNames.Each<global::Hex.Configs.BuffConfig>(() => ability.clyp?.cvmo, () => ability.clyp?.cvmp, () => ability.clyp?.cvmq);

        // ability -> cast configs: eji.beuu().config, eji.beuu().data, eji.beuv().config, eji.beuv().data, eji.clyl.config, eji.clyl.data, eji.clym.config, eji.clym.data, eji.clyn.config, eji.clyn.data, eji.clyp.damageDealer, eji.clyp.selfDealer, eji.clyp.allyDealer
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Configs.CastDealerConfig> CastConfigs => GameNames.Each<global::Hex.Configs.CastDealerConfig>(() => ability.beuu()?.config, () => ability.beuu()?.data, () => ability.beuv()?.config, () => ability.beuv()?.data, () => ability.clyl?.config, () => ability.clyl?.data, () => ability.clym?.config, () => ability.clym?.data, () => ability.clyn?.config, () => ability.clyn?.data, () => ability.clyp?.damageDealer, () => ability.clyp?.selfDealer, () => ability.clyp?.allyDealer);

        // ability -> AbilityLogicConfig: eji.clyp : AbilityLogicConfig
        internal global::Hex.Configs.AbilityLogicConfig LogicConfig => ability.clyp;
    }

    extension(GameBattleLogic battleLogic)
    {
        // battle logic: the defender answers before the strike: eqe.bhpy(Unit) : Boolean
        internal bool AnswersBeforeStrike(global::Hex.Session.Battle.Unit a0) => battleLogic.bhpy(a0);

        // battle logic: a second strike after the counter: eqe.bhpz(Unit, eji) : Boolean
        internal bool StrikesAgainAfterCounter(global::Hex.Session.Battle.Unit a0, GameAbility a1) => battleLogic.bhpz(a0, a1);
    }

    extension(GameBonus bonus)
    {
        // bonus -> receiver: dwa.cjew : dwc
        internal GameBonusReceiver Receiver => bonus.cjew;
    }

    extension(GameBonusReceiver bonusReceiver)
    {
        // bonus receiver add: dwc.bavj(dwa, Boolean) : Void
        internal void Add(GameBonus a0, bool a1) => bonusReceiver.bavj(a0, a1);
    }

    extension(GameBuffs buffs)
    {
        // buffs -> lists: elg.cmgf, elg.cmgg, elg.cmgi
        internal global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.List<GameBuff>> Lists => GameNames.Each<global::Il2CppSystem.Collections.Generic.List<GameBuff>>(() => buffs.cmgf, () => buffs.cmgg, () => buffs.cmgi);
    }

    extension(GameCityScenes cityScenes)
    {
        // town scenes: registry: eja.cluy : cht
        internal GameSceneRegistry Registry => cityScenes.cluy;
    }

    extension(GameDataRoot dataRoot)
    {
        // data root enabled: bko.byxm : Boolean
        internal bool Enabled { get => dataRoot.byxm; set => dataRoot.byxm = value; }

        // data root folder: bko.byxo : String
        internal string Folder { get => dataRoot.byxo; set => dataRoot.byxo = value; }

        // data root name: bko.byxn : String
        internal string Name { get => dataRoot.byxn; set => dataRoot.byxn = value; }
    }

    extension(GameDwelling dwelling)
    {
        // dwelling -> bonuses: fpq.crio : List`1
        internal global::Il2CppSystem.Collections.Generic.List<GameBonus> Bonuses => dwelling.crio;
    }

    extension(GameEntity entity)
    {
        // meeting check: fhf.bmds(fhf) : Boolean
        internal bool Meets(GameEntity a0) => entity.bmds(a0);
    }

    extension(GameForecast forecast)
    {
        // forecast: damage max: exv.cpac : Int32
        internal int DamageMax { get => forecast.cpac; set => forecast.cpac = value; }

        // forecast: damage min: exv.cpab : Int32
        internal int DamageMin { get => forecast.cpab; set => forecast.cpab = value; }

        // forecast: killed max: exv.cpah : Int32
        internal int KilledMax { get => forecast.cpah; set => forecast.cpah = value; }

        // forecast: killed min: exv.cpag : Int32
        internal int KilledMin { get => forecast.cpag; set => forecast.cpag = value; }
    }

    extension(GameHero hero)
    {
        // hero strength: fgd.blwc(Boolean) : Int32
        internal int ArmyStrength(bool a0) => hero.blwc(a0);

        // hero -> its data (Hex.Session.Data.Hero, with the party): fgd.cwts : Hero
        internal global::Hex.Session.Data.Hero Data => hero.cwts;

        // hero -> entity: fgd.cqey : fhf
        internal GameEntity Entity => hero.cqey;

        // hero -> HeroStat paths: fgd.cqep.cjqy, fgd.cqep.cjqz, fgd.cwts.statsByLevel, fgd.cwts.additionalStats, fgd.cwtt, fgd.cwtu
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Session.Data.HeroStat> HeroStats => GameNames.Each<global::Hex.Session.Data.HeroStat>(() => hero.cqep?.cjqy, () => hero.cqep?.cjqz, () => hero.cwts?.statsByLevel, () => hero.cwts?.additionalStats, () => hero.cwtt, () => hero.cwtu);

        // hero -> Logic.Hero: fgd.cqep : Hero
        internal global::Hex.Session.Logic.Hero Logic => hero.cqep;

        // hero -> side wrapper: fgd.cqeq : fqu
        internal GameSide SideOwner => hero.cqeq;
    }

    extension(GameInputBoxOptions inputBoxOptions)
    {
        // input box cancel text: dck.chgg : String
        internal string CancelText { get => inputBoxOptions.chgg; set => inputBoxOptions.chgg = value; }

        // input box caption: dck.chgd : String
        internal string Caption { get => inputBoxOptions.chgd; set => inputBoxOptions.chgd = value; }

        // input box confirm text: dck.chgf : String
        internal string ConfirmText { get => inputBoxOptions.chgf; set => inputBoxOptions.chgf = value; }

        // input box on cancel: dck.chgl : Action
        internal global::Il2CppSystem.Action OnCancel { get => inputBoxOptions.chgl; set => inputBoxOptions.chgl = value; }

        // input box on confirm: dck.chgm : Action`1
        internal global::Il2CppSystem.Action<string> OnConfirm { get => inputBoxOptions.chgm; set => inputBoxOptions.chgm = value; }

        // input box placeholder: dck.chge : String
        internal string Placeholder { get => inputBoxOptions.chge; set => inputBoxOptions.chge = value; }
    }

    extension(GameInputReceiver inputReceiver)
    {
        // bind key: cbe.qpo(EInputAction, EInputState, Action) : Void
        internal void Bind(global::Hex.InputSys.EInputAction a0, global::Hex.InputSys.EInputState a1, global::Il2CppSystem.Action a2) => inputReceiver.qpo(a0, a1, a2);
    }

    extension(GameIntroStep introStep)
    {
        // intro videos step done: ccj.ccge : Boolean
        internal bool Done { get => introStep.ccge; set => introStep.ccge = value; }
    }

    extension(GameLocalization localization)
    {
        // text of a key: cav.qmk(String) : String
        internal string Translate(string a0) => localization.qmk(a0);
    }

    extension(GameMapGameSession mapGameSession)
    {
        // session -> map data: dcz.chmm : Data
        internal global::Hex.Session.Data.Data MapData => mapGameSession.chmm;

        // map session: its scenes: dcz.chmo : dkh
        internal GameSessionScenes Scenes => mapGameSession.chmo;
    }

    extension(GameMapObject mapObject)
    {
        // map object -> model: fql.crkv : bye
        internal GameModelHolder Model => mapObject.crkv;

        // map object -> DataObject: fql.crkw : DataObject
        internal global::Hex.Session.Data.DataObject ObjectData => mapObject.crkw;

        // map object -> owner side: fql.crky : fqu
        internal GameSide Owner => mapObject.crky;

        // map object -> party: fql.crkx : eco
        internal GameParty Party => mapObject.crkx;

        // map object visited by side: fql.bnpb(fqu) : Boolean
        internal bool VisitedBy(GameSide a0) => mapObject.bnpb(a0);
    }

    extension(GameMapObjectStore mapObjectStore)
    {
        // map objects by id: fqm.crlm : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<int, GameMapObject> ById => mapObjectStore.crlm;
    }

    extension(GameMapSession mapSession)
    {
        // map session -> map objects: feh.cpyk : fqm
        internal GameMapObjectStore MapObjects => mapSession.cpyk;

        // map session -> squads: feh.cpym : fre
        internal GameSquadStore Squads => mapSession.cpym;
    }

    extension(GameMessageBoxButton messageBoxButton)
    {
        // message box button click: dcm.chha : Action
        internal global::Il2CppSystem.Action Click { get => messageBoxButton.chha; set => messageBoxButton.chha = value; }

        // message box button label: dcm.chgz : String
        internal string Label { get => messageBoxButton.chgz; set => messageBoxButton.chgz = value; }
    }

    extension(GameMessageBoxOptions messageBoxOptions)
    {
        // message box cancel button: dcl.chgu : dcm
        internal GameMessageBoxButton Cancel { get => messageBoxOptions.chgu; set => messageBoxOptions.chgu = value; }

        // message box caption: dcl.chgp : String
        internal string Caption { get => messageBoxOptions.chgp; set => messageBoxOptions.chgp = value; }

        // message box confirm button: dcl.chgt : dcm
        internal GameMessageBoxButton Confirm { get => messageBoxOptions.chgt; set => messageBoxOptions.chgt = value; }

        // message box text: dcl.chgq : String
        internal string Text { get => messageBoxOptions.chgq; set => messageBoxOptions.chgq = value; }
    }

    extension(GameModelHolder modelHolder)
    {
        // map object model -> GameObjects: bye._cbot_k__BackingField, bye.cbow, bye.cbot
        internal global::System.Collections.Generic.IEnumerable<global::UnityEngine.GameObject> SceneObjects => GameNames.Each<global::UnityEngine.GameObject>(() => modelHolder._cbot_k__BackingField, () => modelHolder.cbow, () => modelHolder.cbot);
    }

    extension(GameResourceEntry resourceEntry)
    {
        // resource entry: asset: gpu.ctyn : Object
        internal global::UnityEngine.Object Asset { get => resourceEntry.ctyn; set => resourceEntry.ctyn = value; }

        // resource entry: key: gpu.ctym : gpt
        internal GameResourceKey Key { get => resourceEntry.ctym; set => resourceEntry.ctym = value; }
    }

    extension(GameResourceKey resourceKey)
    {
        // resource key: type: gpt.ctyl : Type
        internal global::Il2CppSystem.Type AssetType { get => resourceKey.ctyl; set => resourceKey.ctyl = value; }

        // resource key: bundle: gpt.ctyk : String
        internal string Bundle { get => resourceKey.ctyk; set => resourceKey.ctyk = value; }

        // resource key: path: gpt.ctyj : String
        internal string Path { get => resourceKey.ctyj; set => resourceKey.ctyj = value; }
    }

    extension(GameSaveRowPresenter saveRowPresenter)
    {
        // save row on click: cum.lrp(Action) : Void
        internal void OnClick(global::Il2CppSystem.Action a0) => saveRowPresenter.lrp(a0);

        // save row name: cum.con(String, String) : Void
        internal void ShowName(string a0, string a1) => saveRowPresenter.con(a0, a1);
    }

    extension(GameSceneEntry sceneEntry)
    {
        // scene entry: roots: chs.cclz : List`1
        internal global::Il2CppSystem.Collections.Generic.List<global::UnityEngine.GameObject> Roots { get => sceneEntry.cclz; set => sceneEntry.cclz = value; }

        // scene entry: EScene: chs.cclw : EScene
        internal global::Hex.GameScenes.EScene SceneId { get => sceneEntry.cclw; set => sceneEntry.cclw = value; }

        // scene entry: take the roots: chs.xm() : Void
        internal void TakeRoots() => sceneEntry.xm();

        // scene entry: scene: chs.cclx : Scene
        internal global::UnityEngine.SceneManagement.Scene UnityScene { get => sceneEntry.cclx; set => sceneEntry.cclx = value; }
    }

    extension(GameSceneRegistry sceneRegistry)
    {
        // scene registry: add: cht.rau(Il2CppReferenceArray`1) : Void
        internal void AddScenes(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameSceneEntry> a0) => sceneRegistry.rau(a0);

        // scene registry: scenes: cht.ccma : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<global::Hex.GameScenes.EScene, GameSceneEntry> Scenes => sceneRegistry.ccma;
    }

    extension(GameScreenRegistry screenRegistry)
    {
        // hide screen by name: cqp.fgg(String) : Void
        internal void Hide(string a0) => screenRegistry.fgg(a0);

        // open screen by name: cqp.bxq(String) : BhScreen
        internal global::Hex.UI.BhScreen Open(string a0) => screenRegistry.bxq(a0);

        // open screen: cqp.ujl(BhScreen) : BhScreen
        internal global::Hex.UI.BhScreen OpenScreen(global::Hex.UI.BhScreen a0) => screenRegistry.ujl(a0);

        // open screens: cqp.cepc : List`1
        internal global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.BhScreen> OpenScreens => screenRegistry.cepc;

        // register screen: cqp.ujh(BhScreen) : Void
        internal void Register(global::Hex.UI.BhScreen a0) => screenRegistry.ujh(a0);

        // screen by name: cqp.uji(String) : BhScreen
        internal global::Hex.UI.BhScreen Screen(string a0) => screenRegistry.uji(a0);

        // registered screens: cqp.cepb : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<string, global::Hex.UI.BhScreen> Screens => screenRegistry.cepb;
    }

    extension(GameSession session)
    {
        // session -> how it is played (EPlay): dkd.cwbp : EPlay
        internal global::Hex.EPlay Play => session.cwbp;
    }

    extension(GameSessionScenes sessionScenes)
    {
        // session scenes: town: dkh.ciio : eja
        internal GameCityScenes City => sessionScenes.ciio;
    }

    extension(GameSettingsService settingsService)
    {
        // settings: bak.bwsh : SettingsData
        internal global::Hex.Settings.Data.SettingsData Settings => settingsService.bwsh;
    }

    extension(GameSide side)
    {
        // side -> AI: fqu.crna : fzh
        internal GameSideAi Ai => side.crna;

        // side is a person's: fqu.bnyo() : Boolean
        internal bool IsPerson() => side.cwyk;

        // side -> logic with the bonus receiver: fqu.crmf : edc
        internal GameSideLogic Logic => side.crmf;

        // side wrapper -> Side: fqu.cwxs : Side
        internal global::Hex.Session.Data.Side Side => side.cwxs;

        // side -> state: fqu.crmo : fmr
        internal GameSideState State => side.crmo;
    }

    extension(GameSideLogic sideLogic)
    {
        // side logic -> bonus receiver: edc.cjyv : dyj
        internal GameSideBonusReceiver BonusReceiver => sideLogic.cjyv;
    }

    extension(GameSideState sideState)
    {
        // side state: no flee offers: fmr.cqyo : Boolean
        internal bool NoFleeOffers => sideState.cqyo;
    }

    extension(GameSimpleTabData simpleTabData)
    {
        // simple tab click: dcs.chjc : Action
        internal global::Il2CppSystem.Action Click { get => simpleTabData.chjc; set => simpleTabData.chjc = value; }

        // simple tab label: dcs.chjb : String
        internal string Label { get => simpleTabData.chjb; set => simpleTabData.chjb = value; }
    }

    extension(GameSpriteRegistry spriteRegistry)
    {
        // sprite tables: coi.cdwt, coi.cdwu, coi.cdwv, coi.cdww, coi.cdwx, coi.cdwy, coi.cdwz, coi.cdxa, coi.cdxb, coi.cdxc, coi.cdxd, coi.cdxe, coi.cdxf, coi.cdxg, coi.cdxh, coi.cdxi, coi.cdxj, coi.cdxk, coi.cdxl, coi.cdxm, coi.cdxn, coi.cdxo, coi.cdxp, coi.cdxq, coi.cdxr, coi.cdxs, coi.cdxt, coi.cdxu, coi.cdxv, coi.cdxw, coi.cdxx, coi.cdxy, coi.cdxz, coi.cdya, coi.cdyb, coi.cdyc, coi.cdyd, coi.cdye, coi.cdyf, coi.cdyg, coi.cdyh
        internal global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>> Tables => GameNames.Each<global::Il2CppSystem.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>>(() => spriteRegistry.cdwt, () => spriteRegistry.cdwu, () => spriteRegistry.cdwv, () => spriteRegistry.cdww, () => spriteRegistry.cdwx, () => spriteRegistry.cdwy, () => spriteRegistry.cdwz, () => spriteRegistry.cdxa, () => spriteRegistry.cdxb, () => spriteRegistry.cdxc, () => spriteRegistry.cdxd, () => spriteRegistry.cdxe, () => spriteRegistry.cdxf, () => spriteRegistry.cdxg, () => spriteRegistry.cdxh, () => spriteRegistry.cdxi, () => spriteRegistry.cdxj, () => spriteRegistry.cdxk, () => spriteRegistry.cdxl, () => spriteRegistry.cdxm, () => spriteRegistry.cdxn, () => spriteRegistry.cdxo, () => spriteRegistry.cdxp, () => spriteRegistry.cdxq, () => spriteRegistry.cdxr, () => spriteRegistry.cdxs, () => spriteRegistry.cdxt, () => spriteRegistry.cdxu, () => spriteRegistry.cdxv, () => spriteRegistry.cdxw, () => spriteRegistry.cdxx, () => spriteRegistry.cdxy, () => spriteRegistry.cdxz, () => spriteRegistry.cdya, () => spriteRegistry.cdyb, () => spriteRegistry.cdyc, () => spriteRegistry.cdyd, () => spriteRegistry.cdye, () => spriteRegistry.cdyf, () => spriteRegistry.cdyg, () => spriteRegistry.cdyh);
    }

    extension(GameSquad squad)
    {
        // squad -> entity: frc.crot : fhf
        internal GameEntity Entity => squad.crot;

        // squad -> DataSquad: frc.croi : DataSquad
        internal global::Hex.Session.Data.DataSquad SquadData => squad.croi;

        // squad -> hero-like owners: frc.crox, frc.cwyq
        internal global::System.Collections.Generic.IEnumerable<GameHero> SquadOwners => GameNames.Each<GameHero>(() => squad.crox, () => squad.cwyq);

        // squad strength: frc.boej() : Int32
        internal int SquadStrength() => squad.boej();

        // squad -> view: frc.croo : fno
        internal GameSquadView View => squad.croo;
    }

    extension(GameSquadEntity squadEntity)
    {
        // squad entity -> squad: fgy.cqhl : frc
        internal GameSquad Squad => squadEntity.cqhl;
    }

    extension(GameSquadStore squadStore)
    {
        // squads by id: fre.crpe : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<int, GameSquad> ById => squadStore.crpe;
    }

    extension(GameSquadView squadView)
    {
        // squad view -> GameObject: fno.crcq : GameObject
        internal global::UnityEngine.GameObject Model => squadView.crcq;
    }

    extension(GameUnitBase unitBase)
    {
        // Unit -> ability container: eon.bgxp() : ejh
        internal GameAbilities Abilities() => unitBase.cncs;

        // Unit -> buffs: eon.cwoc : elg
        internal GameBuffs Buffs => unitBase.cwoc;
    }

    extension(global::Hex.Configs.UnitPassiveConfig it)
    {
        // passive -> buff data: UnitPassiveConfig.data
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffData> BuffDatas => GameNames.Each<global::Hex.Configs.BuffData>(() => it.data);
    }

    extension(global::Hex.GameHub.UICommon.BhTab it)
    {
        // hub tab click: BhTab.ccmk : Action`1
        internal global::Il2CppSystem.Action<GameTabItem> Click { get => it.ccmk; set => it.ccmk = value; }

        // hub tab available: BhTab.jis(Boolean) : Void
        public void SetAvailable(bool a0) => it.jis(a0);

        // hub tab selected look: BhTab.jit(Boolean) : Void
        public void ShowSelected(bool a0) => it.jit(a0);
    }

    extension(global::Hex.MainMenu.MainMenuNavigationView it)
    {
        // main menu open sub-menu: MainMenuNavigationView.ccbu : MainMenuSubNavView
        public global::Hex.MainMenu.MainMenuSubNavView OpenSubMenu => it.ccbu;

        // main menu select: MainMenuNavigationView.qjp(cab) : Boolean
        internal bool Select(GameMenuSelectable a0) => it.qjp(a0);
    }

    extension(global::Hex.ResManager it)
    {
        // resource table: ResManager.ctyz : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<GameResourceKey, GameResourceEntry> Assets => it.ctyz;
    }

    extension(global::Hex.Session.Battle.Unit it)
    {
        // Unit -> UnitData: Unit.cphd : UnitData
        public global::Hex.Session.Battle.UnitData Data => it.cphd;

        // unit: has a sequence effect: Unit.bjny(String) : Boolean
        public bool HasSequenceEffect(string a0) => it.bjny(a0);

        // Unit -> UnitLogicConfig: Unit.cphb : UnitLogicConfig
        public global::Hex.Configs.UnitLogicConfig LogicConfig => it.cphb;
    }

    extension(global::Hex.Session.Battle.UnitData it)
    {
        // UnitData: top creature's health: UnitData.cpml : Int32
        public int TopHealth { get => it.cpml; set => it.cpml = value; }
    }

    extension(global::Hex.Session.Data.DataFogOfWar it)
    {
        // fog: cell hidden: DataFogOfWar.bkim(Int32) : Boolean
        public bool InFog(int a0) => it.bkim(a0);
    }

    extension(global::Hex.Session.Data.DataObject it)
    {
        // map object data -> configs: DataObject.cpvb, DataObject.cwsw
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.ObjConfigBase> Configs => GameNames.Each<global::Hex.Configs.ObjConfigBase>(() => it.cpvb, () => it.cwsw);
    }

    extension(global::Hex.Session.Data.Hero it)
    {
        // party wrapper -> DataParty: Hero.party : DataParty
        public global::Hex.Session.Data.DataParty Army => it.party;
    }

    extension(global::Hex.Session.Hud.BhHeroHUDPanel it)
    {
        // HUD hero panel -> hero: BhHeroHUDPanel.ciky : fgd
        internal GameHero Hero => it.ciky;
    }

    extension(global::Hex.Session.Logic.Hero it)
    {
        // hero visit id: Hero.cwfh : Int32
        public int VisitId => it.cwfh;
    }

    extension(global::Hex.Settings.UI.BhSettingsCheckbox it)
    {
        // switch callback: BhSettingsCheckbox.bwqs : Action`1
        public global::Il2CppSystem.Action<bool> SwitchCallback { get => it.bwqs; set => it.bwqs = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsSection it)
    {
        // section type: BhSettingsSection.bwqw : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionType => it.bwqw;

        // section type field: BhSettingsSection._bwqw_k__BackingField : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionTypeStored { get => it._bwqw_k__BackingField; set => it._bwqw_k__BackingField = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsTab it)
    {
        // tab click action: BhSettingsTab.bwrc : Action`1
        public global::Il2CppSystem.Action<global::Hex.Settings.UI.ESettingsType> ClickAction { get => it.bwrc; set => it.bwrc = value; }

        // tab selected look: BhSettingsTab.kqd(Boolean) : Void
        public void ShowSelected(bool a0) => it.kqd(a0);
    }

    extension(global::Hex.Settings.UI.SettingsSectionsView it)
    {
        // shown section: SettingsSectionsView.bwry : BhSettingsSection
        public global::Hex.Settings.UI.BhSettingsSection ShownSection => it.bwry;

        // settings show section: SettingsSectionsView.ktl(ESettingsType) : Void
        public void ShowSection(global::Hex.Settings.UI.ESettingsType a0) => it.ktl(a0);
    }

    extension(global::Hex.Storage.IconData it)
    {
        // PNG to sprite: IconData.jvk() : Sprite
        public global::UnityEngine.Sprite ToSprite() => it.jvk();
    }

    extension(global::Hex.UI.BhGenericButton it)
    {
        // button available: BhGenericButton.uhb(Boolean) : Void
        public void SetAvailable(bool a0) => it.uhb(a0);
    }

    extension(global::Hex.UI.BhScreen it)
    {
        // screen hidden at once: BhScreen.udn() : Void
        public void HideAtOnce() => it.udn();

        // screen input: BhScreen.cekv : cbe
        internal GameInputReceiver Input => it.cekv;

        // screen is open: BhScreen.ceks : Boolean
        public bool IsOpen => it.ceks;

        // screen one-time init: BhScreen.hbq() : Void
        public void SetUpOnce() => it.hbq();

        // screen when closed: BhScreen.cekn : Action
        public global::Il2CppSystem.Action WhenClosed { get => it.cekn; set => it.cekn = value; }
    }

    extension(global::Hex.UI.BhSkulls it)
    {
        // BhSkulls level colour: BhSkulls.umo(Int32) : Color
        public global::UnityEngine.Color DangerColor(int a0) => it.umo(a0);

        // BhSkulls level (hero power, squad power): BhSkulls.umn(Int32, Int32) : Int32
        public int DangerLevel(int a0, int a1) => it.umn(a0, a1);

        // BhSkulls fill for a party: BhSkulls.umm(DataParty) : Int32
        public int ShowDangerOfParty(global::Hex.Session.Data.DataParty a0) => it.umm(a0);

        // BhSkulls fill for a squad: BhSkulls.uml(frc) : Int32
        internal int ShowDangerOfSquad(GameSquad a0) => it.uml(a0);
    }

    extension(global::Hex.UI.BhTooltipGarnison it)
    {
        // BhTooltipGarnison fill (party, bool): BhTooltipGarnison.unh(eco, Boolean) : Void
        internal void FillWith(GameParty a0, bool a1) => it.unh(a0, a1);
    }

    extension(global::Hex.UI.GeneralKit.BhCheckbox it)
    {
        // switch is on: BhCheckbox.xyy() : Boolean
        public bool SwitchedOn() => it.xyy();
    }

    extension(global::Hex.UI.GeneralKit.BhNavigationButton it)
    {
        // menu button click: BhNavigationButton.chbo : Action
        public global::Il2CppSystem.Action MenuClick { get => it.chbo; set => it.chbo = value; }
    }

    extension(global::Hex.UI.GeneralKit.BhTag it)
    {
        // tag chip colour: BhTag.yer(Color) : Void
        public void ShowColor(global::UnityEngine.Color a0) => it.yer(a0);

        // tag chip text: BhTag.yep(String) : Void
        public void ShowText(string a0) => it.yep(a0);
    }

    extension(global::Hex.UI.GeneralKit.BhToggle it)
    {
        // check mark show value: BhToggle.yet(Boolean) : Void
        public void ShowValue(bool a0) => it.yet(a0);
    }

    extension(global::Hex.UI.SaveLoad.BhFilter it)
    {
        // save filter elements: BhFilter.cfaz : List`1
        public global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.SaveLoad.BhFilterElement> Elements => it.cfaz;

        // save filter set up: BhFilter.uxg(Il2CppStringArray, Action) : Void
        public void SetUp(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray a0, global::Il2CppSystem.Action a1) => it.uxg(a0, a1);
    }

    extension(global::Hex.UI.SaveLoad.BhFilterElement it)
    {
        // save filter element ticked: BhFilterElement.uxm() : Boolean
        public bool Ticked() => it.uxm();
    }

    extension(global::Hex.UI.SaveLoad.BhSessionInfoElement it)
    {
        // save row presenter: BhSessionInfoElement.vfa() : cum
        internal GameSaveRowPresenter Presenter() => it.vfa();

        // save row campaign shown: BhSessionInfoElement.vfi(Boolean) : Void
        public void ShowCampaign(bool a0) => it.vfi(a0);

        // save row icon: BhSessionInfoElement.vfd(Sprite) : Void
        public void ShowIcon(global::UnityEngine.Sprite a0) => it.vfd(a0);

        // save row cloud: BhSessionInfoElement.vfc(Boolean) : Void
        public void ShowInCloud(bool a0) => it.vfc(a0);

        // save row map icons shown: BhSessionInfoElement.vfl(Boolean) : Void
        public void ShowMapIcons(bool a0) => it.vfl(a0);

        // save row selected look: BhSessionInfoElement.vfn(Boolean) : Void
        public void ShowSelected(bool a0) => it.vfn(a0);

        // save row tag: BhSessionInfoElement.vff(Int32, String, Color) : Void
        public void ShowTag(int a0, string a1, global::UnityEngine.Color a2) => it.vff(a0, a1, a2);

        // save row tag shown: BhSessionInfoElement.vfg(Int32, Boolean) : Void
        public void ShowTagShown(int a0, bool a1) => it.vfg(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltip it)
    {
        // tooltip -> positioner: BhTooltip.cfsh : cxg
        internal GameTooltipPositioner Positioner => it.cfsh;

        // tooltip set texts: BhTooltip.vzn(BasicTooltip, crv) : Void
        internal void SetTexts(global::Hex.UI.BasicTooltip a0, GameTooltipOptions a1) => it.vzn(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipHire it)
    {
        // BhTooltipHire fill (map object): BhTooltipHire.wac(fpq) : Void
        internal void FillFor(GameDwelling a0) => it.wac(a0);
    }

    extension(global::Hex.UI.Tooltips.BhTooltips it)
    {
        // text tooltip: BhTooltips.wbt(String, String, String, RectTransform, crv, ESnapDirection, Boolean) : Void
        internal void ShowTexts(string a0, string a1, string a2, global::UnityEngine.RectTransform a3, GameTooltipOptions a4, global::Hex.UI.Tooltips.ESnapDirection a5, bool a6) => it.wbt(a0, a1, a2, a3, a4, a5, a6);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipSquad it)
    {
        // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.wei(ESquadTooltipType, Object) : Void
        public void FillFor(global::Hex.UI.Tooltips.ESquadTooltipType a0, global::Il2CppSystem.Object a1) => it.wei(a0, a1);
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

    // counter decision: eqe.bhqi(Unit, Unit, eji, Hex, List`1) : Boolean
    public static readonly global::System.Reflection.MethodInfo CounterDecision = M(typeof(GameBattleLogic), nameof(GameBattleLogic.bhqi), typeof(global::Hex.Session.Battle.Unit), typeof(global::Hex.Session.Battle.Unit), typeof(GameAbility), typeof(global::Hex.Session.Battle.Hex), typeof(global::Il2CppSystem.Collections.Generic.List<global::Hex.Session.Battle.Hex>));

    // data roots builders: bkp.jtv() : Void, bkp.cdf() : Void, bkp.lpu() : Void, bkp.esp() : Void, bkp.nbw() : Void
    public static readonly global::System.Reflection.MethodInfo[] DataRootsBuilders =
    {
        M(typeof(global::bkp), nameof(global::bkp.jtv)),
        M(typeof(global::bkp), nameof(global::bkp.cdf)),
        M(typeof(global::bkp), nameof(global::bkp.lpu)),
        M(typeof(global::bkp), nameof(global::bkp.esp)),
        M(typeof(global::bkp), nameof(global::bkp.nbw)),
    };

    // encounter: dkw.zkj(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo Encounter = M(typeof(global::dkw), nameof(global::dkw.zkj), typeof(GameHero), typeof(GameSquad));

    // encounter actions: fhu.bmeq(ELockType, fhf, fhf) : Void, efh.bcuo(frc, fgd) : Void, efh.bcvd(frc, fgd) : Void, dkw.zki(frc, fgd) : Void, dkw.zkk(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo[] EncounterActions =
    {
        M(typeof(global::fhu), nameof(global::fhu.bmeq), typeof(global::Hex.Session.World.ELockType), typeof(GameEntity), typeof(GameEntity)),
        M(typeof(global::efh), nameof(global::efh.bcuo), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::efh), nameof(global::efh.bcvd), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zki), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zkk), typeof(GameHero), typeof(GameSquad)),
    };

    // encounter battle: dkw.zkk(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterBattle = M(typeof(global::dkw), nameof(global::dkw.zkk), typeof(GameHero), typeof(GameSquad));

    // encounter: joins at once: dkw.zki(frc, fgd) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterJoinAtOnce = M(typeof(global::dkw), nameof(global::dkw.zki), typeof(GameSquad), typeof(GameHero));

    // forecast builders: exw.nmb(DamageUiParams, Boolean) : exv, exw.bjfi(DamageUiParams, Boolean) : exv, exw.bar(DamageUiParams, Boolean) : exv
    public static readonly global::System.Reflection.MethodInfo[] ForecastBuilders =
    {
        M(typeof(global::exw), nameof(global::exw.nmb), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.bjfi), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.bar), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
    };

    // BhTooltipHire fill (map object): BhTooltipHire.wac(fpq) : Void
    public static readonly global::System.Reflection.MethodInfo HireTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipHire), nameof(global::Hex.UI.Tooltips.BhTooltipHire.wac), typeof(GameDwelling));

    // intro videos step: ccj.qrx() : Void
    public static readonly global::System.Reflection.MethodInfo IntroVideos = M(typeof(GameIntroStep), nameof(GameIntroStep.qrx));

    // localized text: language changed: cpz.ihb() : Void
    public static readonly global::System.Reflection.MethodInfo LanguageChanged = M(typeof(GameLocalizedText), nameof(GameLocalizedText.ihb));

    // other forecast producers: none on this version
    public static readonly global::System.Reflection.MethodInfo[] OtherForecastMakers = global::System.Array.Empty<global::System.Reflection.MethodInfo>();

    // outline colors to the shader: bei.lks(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.hsw(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.fsj(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.kby(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.cha(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void
    public static readonly global::System.Reflection.MethodInfo[] OutlineColorsToShader =
    {
        M(typeof(global::bei), nameof(global::bei.lks), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.hsw), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.fsj), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.kby), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.cha), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
    };

    // screen registry init: cqp.Init() : Void
    public static readonly global::System.Reflection.MethodInfo ScreenRegistryInit = M(typeof(GameScreenRegistry), nameof(GameScreenRegistry.Init));

    // settings section fills: BhSettingsSection.Start() : Void, BhSettingsSection.Show(SettingsData) : Void, BhSettingsSection.kpk(SettingsData) : Void, BhSettingsSection.kpl(SettingsData) : Void
    public static readonly global::System.Reflection.MethodInfo[] SettingsSectionFills =
    {
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.Start)),
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.Show), typeof(global::Hex.Settings.Data.SettingsData)),
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.kpk), typeof(global::Hex.Settings.Data.SettingsData)),
        M(typeof(global::Hex.Settings.UI.BhSettingsSection), nameof(global::Hex.Settings.UI.BhSettingsSection.kpl), typeof(global::Hex.Settings.Data.SettingsData)),
    };

    // settings show section: SettingsSectionsView.ktl(ESettingsType) : Void
    public static readonly global::System.Reflection.MethodInfo SettingsShowSection = M(typeof(global::Hex.Settings.UI.SettingsSectionsView), nameof(global::Hex.Settings.UI.SettingsSectionsView.ktl), typeof(global::Hex.Settings.UI.ESettingsType));

    // BhSkulls fill for a party: BhSkulls.umm(DataParty) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForParty = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umm), typeof(global::Hex.Session.Data.DataParty));

    // BhSkulls fill for a squad: BhSkulls.uml(frc) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForSquad = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.uml), typeof(GameSquad));

    // BhSkulls level (hero power, squad power): BhSkulls.umn(Int32, Int32) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsLevel = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umn), typeof(int), typeof(int));

    // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.wei(ESquadTooltipType, Object) : Void
    public static readonly global::System.Reflection.MethodInfo SquadTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipSquad), nameof(global::Hex.UI.Tooltips.BhTooltipSquad.wei), typeof(global::Hex.UI.Tooltips.ESquadTooltipType), typeof(global::Il2CppSystem.Object));

    // positioner place for point: cxg.wdq(Vector2) : Vector2, cwn.wdq(Vector2) : Vector2, cwr.wdq(Vector2) : Vector2
    public static readonly global::System.Reflection.MethodInfo[] TooltipPlaceForPoint =
    {
        M(typeof(GameTooltipPositioner), nameof(GameTooltipPositioner.wdq), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwn), nameof(global::cwn.wdq), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwr), nameof(global::cwr.wdq), typeof(global::UnityEngine.Vector2)),
    };

    // tooltip set texts: BhTooltip.vzn(BasicTooltip, crv) : Void
    public static readonly global::System.Reflection.MethodInfo TooltipSetTexts = M(typeof(global::Hex.UI.Tooltips.BhTooltip), nameof(global::Hex.UI.Tooltips.BhTooltip.vzn), typeof(global::Hex.UI.BasicTooltip), typeof(GameTooltipOptions));
}
