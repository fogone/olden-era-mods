// Generated for the game 0.82.07 (tools/game-names from the game's files, or the research plugin's `aliases`). Do not edit:
// make it again (docs/game-names.md). The lookups behind each name are the Lookup.Generate ones in Game*.cs,
// the choices among several candidates are in GameNames.pins.txt.

global using GameAbilities = global::ejh;
global using GameAbility = global::eji;
global using GameBattleLogic = global::eqe;
global using GameBonus = global::dwa;
global using GameBonusReceiver = global::dwc;
global using GameBuff = global::ele;
global using GameBuffs = global::elg;
global using GameCaster = global::elm;
global using GameCastSource = global::eln;
global using GameCastTarget = global::elq;
global using GameDwelling = global::fpq;
global using GameEntity = global::fhf;
global using GameForecast = global::exv;
global using GameHero = global::fgd;
global using GameInputBoxOptions = global::dck;
global using GameInputReceiver = global::cbe;
global using GameIntroStep = global::ccj;
global using GameLocalization = global::cav;
global using GameLocalizedText = global::cpz;
global using GameMapGameSession = global::dcz;
global using GameMapObject = global::fql;
global using GameMapObjectStore = global::fqm;
global using GameMapSession = global::feh;
global using GameMenuSelectable = global::cab;
global using GameMessageBoxButton = global::dcm;
global using GameMessageBoxOptions = global::dcl;
global using GameModelHolder = global::bye;
global using GameParty = global::eco;
global using GameSaveRowPresenter = global::cum;
global using GameScreenRegistry = global::cqp;
global using GameSession = global::dkd;
global using GameSettingsService = global::bak;
global using GameSide = global::fqu;
global using GameSideAi = global::fzh;
global using GameSideBonusReceiver = global::dyj;
global using GameSideLogic = global::edc;
global using GameSideState = global::fmr;
global using GameSimpleTabData = global::dcs;
global using GameSquad = global::frc;
global using GameSquadEntity = global::fgy;
global using GameSquadStore = global::fre;
global using GameSquadView = global::fno;
global using GameTabItem = global::cia;
global using GameTooltipOptions = global::crv;
global using GameTooltipPositioner = global::cxg;
global using GameUnitBase = global::eon;

namespace OldenEra.Modding.Common;

internal static class GameNames
{
    public const string GameVersion = "0.82.07";

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

    // always battle (static): lh.bvdw : Boolean
    public static bool AlwaysBattle => global::lh.bvdw;

    // forecast builder: exw.bkg(DamageUiParams, Boolean) : exv
    public static GameForecast BuildForecast(global::Hex.Session.Battle.DamageUiParams a0, bool a1) => global::exw.bkg(a0, a1);

    // encounter: dkw.zki(fgd, frc) : Void
    public static void Encounter(GameHero a0, GameSquad a1) => global::dkw.zki(a0, a1);

    // HUD hero panel: BhHeroHUDPanel.me : BhHeroHUDPanel
    public static global::Hex.Session.Hud.BhHeroHUDPanel HudPanel => global::Hex.Session.Hud.BhHeroHUDPanel.me;

    // diplomacy check: ffd.bloq(fgd, frc) : Boolean
    public static bool JoinsByDiplomacy(GameHero a0, GameSquad a1) => global::ffd.bloq(a0, a1);

    // diplomacy check explained: ffd.blop(fgd, frc, String&) : Boolean
    public static bool JoinsByDiplomacyExplained(GameHero a0, GameSquad a1, out string a2) => global::ffd.blop(a0, a1, out a2);

    // join share: ffd.blor(frc, fgd) : Single
    public static float JoinShare(GameSquad a0, GameHero a1) => global::ffd.blor(a0, a1);

    // localization service: iv.buvy : cav
    public static GameLocalization Localization => global::iv.buvy;

    // map session: feh.me : feh
    public static GameMapSession MapSession => global::feh.me;

    // screen registry instance: cqp.ujg() : cqp
    public static GameScreenRegistry ScreenRegistry() => global::cqp.ujg();

    // session: dkd.Me : dkd
    public static GameSession Session => global::dkd.Me;

    // settings service: bak.bwsf : bak
    public static GameSettingsService SettingsService => global::bak.bwsf;

    // tooltips holder: BhTooltips.cftf : BhTooltips
    public static global::Hex.UI.Tooltips.BhTooltips Tooltips => global::Hex.UI.Tooltips.BhTooltips.cftf;

    // unit portraits: efr.bdbb(String) : Sprite
    public static global::UnityEngine.Sprite UnitPortrait(string a0) => global::efr.bdbb(a0);

    extension(GameAbilities abilities)
    {
        // ability container -> ability (EAbilityType): ejh.beth(EAbilityType) : eji
        public GameAbility Ability(global::Hex.Configs.EAbilityType a0) => abilities.beth(a0);
    }

    extension(GameAbility ability)
    {
        // ability -> buff configs: eji.clyl.cvmh, eji.clyl.cvmi, eji.clyl.cvmj
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffConfig> BuffConfigs => GameNames.Each<global::Hex.Configs.BuffConfig>(() => ability.clyl?.cvmh, () => ability.clyl?.cvmi, () => ability.clyl?.cvmj);

        // ability -> cast configs: eji.bevb().config, eji.bevb().data, eji.beva().config, eji.beva().data, eji.clyh.config, eji.clyh.data, eji.clyi.config, eji.clyi.data, eji.clyj.config, eji.clyj.data, eji.clyl.damageDealer, eji.clyl.selfDealer, eji.clyl.allyDealer
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.CastDealerConfig> CastConfigs => GameNames.Each<global::Hex.Configs.CastDealerConfig>(() => ability.bevb()?.config, () => ability.bevb()?.data, () => ability.beva()?.config, () => ability.beva()?.data, () => ability.clyh?.config, () => ability.clyh?.data, () => ability.clyi?.config, () => ability.clyi?.data, () => ability.clyj?.config, () => ability.clyj?.data, () => ability.clyl?.damageDealer, () => ability.clyl?.selfDealer, () => ability.clyl?.allyDealer);

        // ability -> AbilityLogicConfig: eji.clyl : AbilityLogicConfig
        public global::Hex.Configs.AbilityLogicConfig LogicConfig => ability.clyl;
    }

    extension(GameBattleLogic battleLogic)
    {
        // battle logic: the defender answers before the strike: eqe.bhpz(Unit) : Boolean
        public bool AnswersBeforeStrike(global::Hex.Session.Battle.Unit a0) => battleLogic.bhpz(a0);

        // battle logic: a second strike after the counter: eqe.bhqa(Unit, eji) : Boolean
        public bool StrikesAgainAfterCounter(global::Hex.Session.Battle.Unit a0, GameAbility a1) => battleLogic.bhqa(a0, a1);
    }

    extension(GameBonus bonus)
    {
        // bonus -> receiver: dwa.cjes : dwc
        public GameBonusReceiver Receiver => bonus.cjes;
    }

    extension(GameBonusReceiver bonusReceiver)
    {
        // bonus receiver add: dwc.bavk(dwa, Boolean) : Void
        public void Add(GameBonus a0, bool a1) => bonusReceiver.bavk(a0, a1);
    }

    extension(GameBuffs buffs)
    {
        // buffs -> lists: elg.cmgb, elg.cmgc, elg.cmgd
        public global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.List<GameBuff>> Lists => GameNames.Each<global::Il2CppSystem.Collections.Generic.List<GameBuff>>(() => buffs.cmgb, () => buffs.cmgc, () => buffs.cmgd);
    }

    extension(GameDwelling dwelling)
    {
        // dwelling -> bonuses: fpq.crih : List`1
        public global::Il2CppSystem.Collections.Generic.List<GameBonus> Bonuses => dwelling.crih;
    }

    extension(GameEntity entity)
    {
        // meeting check: fhf.bmdt(fhf) : Boolean
        public bool Meets(GameEntity a0) => entity.bmdt(a0);
    }

    extension(GameForecast forecast)
    {
        // forecast: damage max: exv.cozv : Int32
        public int DamageMax { get => forecast.cozv; set => forecast.cozv = value; }

        // forecast: damage min: exv.cozu : Int32
        public int DamageMin { get => forecast.cozu; set => forecast.cozu = value; }

        // forecast: killed max: exv.cpaa : Int32
        public int KilledMax { get => forecast.cpaa; set => forecast.cpaa = value; }

        // forecast: killed min: exv.cozz : Int32
        public int KilledMin { get => forecast.cozz; set => forecast.cozz = value; }
    }

    extension(GameHero hero)
    {
        // hero strength: fgd.blwd(Boolean) : Int32
        public int ArmyStrength(bool a0) => hero.blwd(a0);

        // hero -> its data (Hex.Session.Data.Hero, with the party): fgd.cwtl : Hero
        public global::Hex.Session.Data.Hero Data => hero.cwtl;

        // hero -> entity: fgd.cqer : fhf
        public GameEntity Entity => hero.cqer;

        // hero -> HeroStat paths: fgd.cqei.cjqu, fgd.cqei.cjqv, fgd.cwtl.statsByLevel, fgd.cwtl.additionalStats, fgd.cwtm, fgd.cwtn
        public global::System.Collections.Generic.IEnumerable<global::Hex.Session.Data.HeroStat> HeroStats => GameNames.Each<global::Hex.Session.Data.HeroStat>(() => hero.cqei?.cjqu, () => hero.cqei?.cjqv, () => hero.cwtl?.statsByLevel, () => hero.cwtl?.additionalStats, () => hero.cwtm, () => hero.cwtn);

        // hero -> Logic.Hero: fgd.cqei : Hero
        public global::Hex.Session.Logic.Hero Logic => hero.cqei;

        // hero -> side wrapper: fgd.cqej : fqu
        public GameSide SideOwner => hero.cqej;
    }

    extension(GameInputBoxOptions inputBoxOptions)
    {
        // input box cancel text: dck.chgc : String
        public string CancelText { get => inputBoxOptions.chgc; set => inputBoxOptions.chgc = value; }

        // input box caption: dck.chfz : String
        public string Caption { get => inputBoxOptions.chfz; set => inputBoxOptions.chfz = value; }

        // input box confirm text: dck.chgb : String
        public string ConfirmText { get => inputBoxOptions.chgb; set => inputBoxOptions.chgb = value; }

        // input box on cancel: dck.chgh : Action
        public global::Il2CppSystem.Action OnCancel { get => inputBoxOptions.chgh; set => inputBoxOptions.chgh = value; }

        // input box on confirm: dck.chgi : Action`1
        public global::Il2CppSystem.Action<string> OnConfirm { get => inputBoxOptions.chgi; set => inputBoxOptions.chgi = value; }

        // input box placeholder: dck.chga : String
        public string Placeholder { get => inputBoxOptions.chga; set => inputBoxOptions.chga = value; }
    }

    extension(GameInputReceiver inputReceiver)
    {
        // bind key: cbe.qpo(EInputAction, EInputState, Action) : Void
        public void Bind(global::Hex.InputSys.EInputAction a0, global::Hex.InputSys.EInputState a1, global::Il2CppSystem.Action a2) => inputReceiver.qpo(a0, a1, a2);
    }

    extension(GameIntroStep introStep)
    {
        // intro videos step done: ccj.ccgc : Boolean
        public bool Done { get => introStep.ccgc; set => introStep.ccgc = value; }
    }

    extension(GameLocalization localization)
    {
        // text of a key: cav.qmk(String) : String
        public string Translate(string a0) => localization.qmk(a0);
    }

    extension(GameMapGameSession mapGameSession)
    {
        // session -> map data: dcz.chmi : Data
        public global::Hex.Session.Data.Data MapData => mapGameSession.chmi;
    }

    extension(GameMapObject mapObject)
    {
        // map object -> model: fql.crko : bye
        public GameModelHolder Model => mapObject.crko;

        // map object -> DataObject: fql.crkp : DataObject
        public global::Hex.Session.Data.DataObject ObjectData => mapObject.crkp;

        // map object -> owner side: fql.crkr : fqu
        public GameSide Owner => mapObject.crkr;

        // map object -> party: fql.crkq : eco
        public GameParty Party => mapObject.crkq;

        // map object visited by side: fql.bnpc(fqu) : Boolean
        public bool VisitedBy(GameSide a0) => mapObject.bnpc(a0);
    }

    extension(GameMapObjectStore mapObjectStore)
    {
        // map objects by id: fqm.crlf : Dictionary`2
        public global::Il2CppSystem.Collections.Generic.Dictionary<int, GameMapObject> ById => mapObjectStore.crlf;
    }

    extension(GameMapSession mapSession)
    {
        // map session -> map objects: feh.cpyd : fqm
        public GameMapObjectStore MapObjects => mapSession.cpyd;

        // map session -> squads: feh.cpyf : fre
        public GameSquadStore Squads => mapSession.cpyf;
    }

    extension(GameMessageBoxButton messageBoxButton)
    {
        // message box button click: dcm.chgw : Action
        public global::Il2CppSystem.Action Click { get => messageBoxButton.chgw; set => messageBoxButton.chgw = value; }

        // message box button label: dcm.chgv : String
        public string Label { get => messageBoxButton.chgv; set => messageBoxButton.chgv = value; }
    }

    extension(GameMessageBoxOptions messageBoxOptions)
    {
        // message box cancel button: dcl.chgq : dcm
        public GameMessageBoxButton Cancel { get => messageBoxOptions.chgq; set => messageBoxOptions.chgq = value; }

        // message box caption: dcl.chgl : String
        public string Caption { get => messageBoxOptions.chgl; set => messageBoxOptions.chgl = value; }

        // message box confirm button: dcl.chgp : dcm
        public GameMessageBoxButton Confirm { get => messageBoxOptions.chgp; set => messageBoxOptions.chgp = value; }

        // message box text: dcl.chgm : String
        public string Text { get => messageBoxOptions.chgm; set => messageBoxOptions.chgm = value; }
    }

    extension(GameModelHolder modelHolder)
    {
        // map object model -> GameObjects: bye._cbor_k__BackingField, bye.cbou, bye.cbor
        public global::System.Collections.Generic.IEnumerable<global::UnityEngine.GameObject> SceneObjects => GameNames.Each<global::UnityEngine.GameObject>(() => modelHolder._cbor_k__BackingField, () => modelHolder.cbou, () => modelHolder.cbor);
    }

    extension(GameSaveRowPresenter saveRowPresenter)
    {
        // save row on click: cum.fkf(Action) : Void
        public void OnClick(global::Il2CppSystem.Action a0) => saveRowPresenter.fkf(a0);

        // save row name: cum.ifs(String, String) : Void
        public void ShowName(string a0, string a1) => saveRowPresenter.ifs(a0, a1);
    }

    extension(GameScreenRegistry screenRegistry)
    {
        // hide screen by name: cqp.ujr(String) : Void
        public void Hide(string a0) => screenRegistry.ujr(a0);

        // open screen by name: cqp.bjv(String) : BhScreen
        public global::Hex.UI.BhScreen Open(string a0) => screenRegistry.bjv(a0);

        // open screen: cqp.ujn(BhScreen) : BhScreen
        public global::Hex.UI.BhScreen OpenScreen(global::Hex.UI.BhScreen a0) => screenRegistry.ujn(a0);

        // open screens: cqp.cepa : List`1
        public global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.BhScreen> OpenScreens => screenRegistry.cepa;

        // register screen: cqp.efg(BhScreen) : Void
        public void Register(global::Hex.UI.BhScreen a0) => screenRegistry.efg(a0);

        // screen by name: cqp.ujk(String) : BhScreen
        public global::Hex.UI.BhScreen Screen(string a0) => screenRegistry.ujk(a0);

        // registered screens: cqp.ceoz : Dictionary`2
        public global::Il2CppSystem.Collections.Generic.Dictionary<string, global::Hex.UI.BhScreen> Screens => screenRegistry.ceoz;
    }

    extension(GameSession session)
    {
        // session -> how it is played (EPlay): dkd.cwbi : EPlay
        public global::Hex.EPlay Play => session.cwbi;
    }

    extension(GameSettingsService settingsService)
    {
        // settings: bak.bwsg : SettingsData
        public global::Hex.Settings.Data.SettingsData Settings => settingsService.bwsg;
    }

    extension(GameSide side)
    {
        // side -> AI: fqu.crmt : fzh
        public GameSideAi Ai => side.crmt;

        // side is a person's: fqu.bnyp() : Boolean
        public bool IsPerson() => side.cwyd;

        // side -> logic with the bonus receiver: fqu.crly : edc
        public GameSideLogic Logic => side.crly;

        // side wrapper -> Side: fqu.cwxl : Side
        public global::Hex.Session.Data.Side Side => side.cwxl;

        // side -> state: fqu.crmh : fmr
        public GameSideState State => side.crmh;
    }

    extension(GameSideLogic sideLogic)
    {
        // side logic -> bonus receiver: edc.cjyr : dyj
        public GameSideBonusReceiver BonusReceiver => sideLogic.cjyr;
    }

    extension(GameSideState sideState)
    {
        // side state: no flee offers: fmr.cqyh : Boolean
        public bool NoFleeOffers => sideState.cqyh;
    }

    extension(GameSimpleTabData simpleTabData)
    {
        // simple tab click: dcs.chiy : Action
        public global::Il2CppSystem.Action Click { get => simpleTabData.chiy; set => simpleTabData.chiy = value; }

        // simple tab label: dcs.chix : String
        public string Label { get => simpleTabData.chix; set => simpleTabData.chix = value; }
    }

    extension(GameSquad squad)
    {
        // squad -> entity: frc.crom : fhf
        public GameEntity Entity => squad.crom;

        // squad -> DataSquad: frc.crob : DataSquad
        public global::Hex.Session.Data.DataSquad SquadData => squad.crob;

        // squad -> hero-like owners: frc.croq, frc.cwyj
        public global::System.Collections.Generic.IEnumerable<GameHero> SquadOwners => GameNames.Each<GameHero>(() => squad.croq, () => squad.cwyj);

        // squad strength: frc.bc() : Int32
        public int SquadStrength() => squad.bc();

        // squad -> view: frc.croh : fno
        public GameSquadView View => squad.croh;
    }

    extension(GameSquadEntity squadEntity)
    {
        // squad entity -> squad: fgy.cqhe : frc
        public GameSquad Squad => squadEntity.cqhe;
    }

    extension(GameSquadStore squadStore)
    {
        // squads by id: fre.crox : Dictionary`2
        public global::Il2CppSystem.Collections.Generic.Dictionary<int, GameSquad> ById => squadStore.crox;
    }

    extension(GameSquadView squadView)
    {
        // squad view -> GameObject: fno.crcj : GameObject
        public global::UnityEngine.GameObject Model => squadView.crcj;
    }

    extension(GameUnitBase unitBase)
    {
        // Unit -> ability container: eon.bgxs() : ejh
        public GameAbilities Abilities() => unitBase.cncn;

        // Unit -> buffs: eon.cwnv : elg
        public GameBuffs Buffs => unitBase.cwnv;
    }

    extension(global::Hex.Configs.UnitPassiveConfig it)
    {
        // passive -> buff data: UnitPassiveConfig.data
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffData> BuffDatas => GameNames.Each<global::Hex.Configs.BuffData>(() => it.data);
    }

    extension(global::Hex.GameHub.UICommon.BhTab it)
    {
        // hub tab click: BhTab.ccmi : Action`1
        public global::Il2CppSystem.Action<GameTabItem> Click { get => it.ccmi; set => it.ccmi = value; }

        // hub tab available: BhTab.jiu(Boolean) : Void
        public void SetAvailable(bool a0) => it.jiu(a0);

        // hub tab selected look: BhTab.jiv(Boolean) : Void
        public void ShowSelected(bool a0) => it.jiv(a0);
    }

    extension(global::Hex.MainMenu.MainMenuNavigationView it)
    {
        // main menu open sub-menu: MainMenuNavigationView.ccbs : MainMenuSubNavView
        public global::Hex.MainMenu.MainMenuSubNavView OpenSubMenu => it.ccbs;

        // main menu select: MainMenuNavigationView.qjp(cab) : Boolean
        public bool Select(GameMenuSelectable a0) => it.qjp(a0);
    }

    extension(global::Hex.Session.Battle.Unit it)
    {
        // Unit -> UnitData: Unit.cpgw : UnitData
        public global::Hex.Session.Battle.UnitData Data => it.cpgw;

        // unit: has a sequence effect: Unit.bjnz(String) : Boolean
        public bool HasSequenceEffect(string a0) => it.bjnz(a0);

        // Unit -> UnitLogicConfig: Unit.cpgu : UnitLogicConfig
        public global::Hex.Configs.UnitLogicConfig LogicConfig => it.cpgu;
    }

    extension(global::Hex.Session.Battle.UnitData it)
    {
        // UnitData: top creature's health: UnitData.cpme : Int32
        public int TopHealth { get => it.cpme; set => it.cpme = value; }
    }

    extension(global::Hex.Session.Data.DataFogOfWar it)
    {
        // fog: cell hidden: DataFogOfWar.bkin(Int32) : Boolean
        public bool InFog(int a0) => it.bkin(a0);
    }

    extension(global::Hex.Session.Data.DataObject it)
    {
        // map object data -> configs: DataObject.cpuu, DataObject.cwsp
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.ObjConfigBase> Configs => GameNames.Each<global::Hex.Configs.ObjConfigBase>(() => it.cpuu, () => it.cwsp);
    }

    extension(global::Hex.Session.Data.Hero it)
    {
        // party wrapper -> DataParty: Hero.party : DataParty
        public global::Hex.Session.Data.DataParty Army => it.party;
    }

    extension(global::Hex.Session.Hud.BhHeroHUDPanel it)
    {
        // HUD hero panel -> hero: BhHeroHUDPanel.ciku : fgd
        public GameHero Hero => it.ciku;
    }

    extension(global::Hex.Session.Logic.Hero it)
    {
        // hero visit id: Hero.cwfa : Int32
        public int VisitId => it.cwfa;
    }

    extension(global::Hex.Settings.UI.BhSettingsCheckbox it)
    {
        // switch callback: BhSettingsCheckbox.bwqr : Action`1
        public global::Il2CppSystem.Action<bool> SwitchCallback { get => it.bwqr; set => it.bwqr = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsSection it)
    {
        // section type: BhSettingsSection.bwqv : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionType => it.bwqv;

        // section type field: BhSettingsSection._bwqv_k__BackingField : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionTypeStored { get => it._bwqv_k__BackingField; set => it._bwqv_k__BackingField = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsTab it)
    {
        // tab click action: BhSettingsTab.bwrb : Action`1
        public global::Il2CppSystem.Action<global::Hex.Settings.UI.ESettingsType> ClickAction { get => it.bwrb; set => it.bwrb = value; }

        // tab selected look: BhSettingsTab.kqf(Boolean) : Void
        public void ShowSelected(bool a0) => it.kqf(a0);
    }

    extension(global::Hex.Settings.UI.SettingsSectionsView it)
    {
        // shown section: SettingsSectionsView.bwrx : BhSettingsSection
        public global::Hex.Settings.UI.BhSettingsSection ShownSection => it.bwrx;
    }

    extension(global::Hex.UI.BhGenericButton it)
    {
        // button available: BhGenericButton.uhb(Boolean) : Void
        public void SetAvailable(bool a0) => it.uhb(a0);
    }

    extension(global::Hex.UI.BhScreen it)
    {
        // screen hidden at once: BhScreen.udm() : Void
        public void HideAtOnce() => it.udm();

        // screen input: BhScreen.cekt : cbe
        public GameInputReceiver Input => it.cekt;

        // screen is open: BhScreen.cekq : Boolean
        public bool IsOpen => it.cekq;

        // screen one-time init: BhScreen.hbq() : Void
        public void SetUpOnce() => it.hbq();

        // screen when closed: BhScreen.cekl : Action
        public global::Il2CppSystem.Action WhenClosed { get => it.cekl; set => it.cekl = value; }
    }

    extension(global::Hex.UI.BhSkulls it)
    {
        // BhSkulls level colour: BhSkulls.umq(Int32) : Color
        public global::UnityEngine.Color DangerColor(int a0) => it.umq(a0);

        // BhSkulls level (hero power, squad power): BhSkulls.ump(Int32, Int32) : Int32
        public int DangerLevel(int a0, int a1) => it.ump(a0, a1);

        // BhSkulls fill for a party: BhSkulls.umo(DataParty) : Int32
        public int ShowDangerOfParty(global::Hex.Session.Data.DataParty a0) => it.umo(a0);

        // BhSkulls fill for a squad: BhSkulls.umn(frc) : Int32
        public int ShowDangerOfSquad(GameSquad a0) => it.umn(a0);
    }

    extension(global::Hex.UI.BhTooltipGarnison it)
    {
        // BhTooltipGarnison fill (party, bool): BhTooltipGarnison.unj(eco, Boolean) : Void
        public void FillWith(GameParty a0, bool a1) => it.unj(a0, a1);
    }

    extension(global::Hex.UI.GeneralKit.BhCheckbox it)
    {
        // switch is on: BhCheckbox.xyx() : Boolean
        public bool SwitchedOn() => it.xyx();
    }

    extension(global::Hex.UI.GeneralKit.BhNavigationButton it)
    {
        // menu button click: BhNavigationButton.chbk : Action
        public global::Il2CppSystem.Action MenuClick { get => it.chbk; set => it.chbk = value; }
    }

    extension(global::Hex.UI.GeneralKit.BhTag it)
    {
        // tag chip colour: BhTag.yeq(Color) : Void
        public void ShowColor(global::UnityEngine.Color a0) => it.yeq(a0);

        // tag chip text: BhTag.yeo(String) : Void
        public void ShowText(string a0) => it.yeo(a0);
    }

    extension(global::Hex.UI.GeneralKit.BhToggle it)
    {
        // check mark show value: BhToggle.yes(Boolean) : Void
        public void ShowValue(bool a0) => it.yes(a0);
    }

    extension(global::Hex.UI.SaveLoad.BhFilter it)
    {
        // save filter elements: BhFilter.cfax : List`1
        public global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.SaveLoad.BhFilterElement> Elements => it.cfax;

        // save filter set up: BhFilter.uxi(Il2CppStringArray, Action) : Void
        public void SetUp(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray a0, global::Il2CppSystem.Action a1) => it.uxi(a0, a1);
    }

    extension(global::Hex.UI.SaveLoad.BhFilterElement it)
    {
        // save filter element ticked: BhFilterElement.uxo() : Boolean
        public bool Ticked() => it.uxo();
    }

    extension(global::Hex.UI.SaveLoad.BhSessionInfoElement it)
    {
        // save row presenter: BhSessionInfoElement.vfc() : cum
        public GameSaveRowPresenter Presenter() => it.vfc();

        // save row campaign shown: BhSessionInfoElement.vfk(Boolean) : Void
        public void ShowCampaign(bool a0) => it.vfk(a0);

        // save row icon: BhSessionInfoElement.vff(Sprite) : Void
        public void ShowIcon(global::UnityEngine.Sprite a0) => it.vff(a0);

        // save row cloud: BhSessionInfoElement.vfe(Boolean) : Void
        public void ShowInCloud(bool a0) => it.vfe(a0);

        // save row map icons shown: BhSessionInfoElement.vfn(Boolean) : Void
        public void ShowMapIcons(bool a0) => it.vfn(a0);

        // save row selected look: BhSessionInfoElement.vfp(Boolean) : Void
        public void ShowSelected(bool a0) => it.vfp(a0);

        // save row tag: BhSessionInfoElement.vfh(Int32, String, Color) : Void
        public void ShowTag(int a0, string a1, global::UnityEngine.Color a2) => it.vfh(a0, a1, a2);

        // save row tag shown: BhSessionInfoElement.vfi(Int32, Boolean) : Void
        public void ShowTagShown(int a0, bool a1) => it.vfi(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltip it)
    {
        // tooltip -> positioner: BhTooltip.cfsf : cxg
        public GameTooltipPositioner Positioner => it.cfsf;

        // tooltip set texts: BhTooltip.vzp(BasicTooltip, crv) : Void
        public void SetTexts(global::Hex.UI.BasicTooltip a0, GameTooltipOptions a1) => it.vzp(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipHire it)
    {
        // BhTooltipHire fill (map object): BhTooltipHire.wae(fpq) : Void
        public void FillFor(GameDwelling a0) => it.wae(a0);
    }

    extension(global::Hex.UI.Tooltips.BhTooltips it)
    {
        // text tooltip: BhTooltips.wbt(String, String, String, RectTransform, crv, ESnapDirection, Boolean) : Void
        public void ShowTexts(string a0, string a1, string a2, global::UnityEngine.RectTransform a3, GameTooltipOptions a4, global::Hex.UI.Tooltips.ESnapDirection a5, bool a6) => it.wbt(a0, a1, a2, a3, a4, a5, a6);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipSquad it)
    {
        // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.weh(ESquadTooltipType, Object) : Void
        public void FillFor(global::Hex.UI.Tooltips.ESquadTooltipType a0, global::Il2CppSystem.Object a1) => it.weh(a0, a1);
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

    // counter decision: eqe.bhqj(Unit, Unit, eji, Hex, List`1) : Boolean
    public static readonly global::System.Reflection.MethodInfo CounterDecision = M(typeof(GameBattleLogic), nameof(GameBattleLogic.bhqj), typeof(global::Hex.Session.Battle.Unit), typeof(global::Hex.Session.Battle.Unit), typeof(GameAbility), typeof(global::Hex.Session.Battle.Hex), typeof(global::Il2CppSystem.Collections.Generic.List<global::Hex.Session.Battle.Hex>));

    // diplomacy points (squad, hero): ffd.blou(fgd, frc, DiplomacyInfoConfig) : Int32, ffd.blow(fgd, frc, DiplomacyInfoConfig) : Int32
    public static readonly global::System.Reflection.MethodInfo[] DiplomacyPoints =
    {
        M(typeof(global::ffd), nameof(global::ffd.blou), typeof(GameHero), typeof(GameSquad), typeof(global::Hex.Configs.DiplomacyInfoConfig)),
        M(typeof(global::ffd), nameof(global::ffd.blow), typeof(GameHero), typeof(GameSquad), typeof(global::Hex.Configs.DiplomacyInfoConfig)),
    };

    // encounter: dkw.zki(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo Encounter = M(typeof(global::dkw), nameof(global::dkw.zki), typeof(GameHero), typeof(GameSquad));

    // encounter actions: fhu.bmer(ELockType, fhf, fhf) : Void, efh.bcup(frc, fgd) : Void, efh.bcve(frc, fgd) : Void, dkw.zkh(frc, fgd) : Void, dkw.zkj(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo[] EncounterActions =
    {
        M(typeof(global::fhu), nameof(global::fhu.bmer), typeof(global::Hex.Session.World.ELockType), typeof(GameEntity), typeof(GameEntity)),
        M(typeof(global::efh), nameof(global::efh.bcup), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::efh), nameof(global::efh.bcve), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zkh), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zkj), typeof(GameHero), typeof(GameSquad)),
    };

    // encounter battle: dkw.zkj(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterBattle = M(typeof(global::dkw), nameof(global::dkw.zkj), typeof(GameHero), typeof(GameSquad));

    // encounter: joins at once: dkw.zkh(frc, fgd) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterJoinAtOnce = M(typeof(global::dkw), nameof(global::dkw.zkh), typeof(GameSquad), typeof(GameHero));

    // forecast builders: exw.bkg(DamageUiParams, Boolean) : exv, exw.isw(DamageUiParams, Boolean) : exv, exw.bjfj(DamageUiParams, Boolean) : exv
    public static readonly global::System.Reflection.MethodInfo[] ForecastBuilders =
    {
        M(typeof(global::exw), nameof(global::exw.bkg), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.isw), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.bjfj), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
    };

    // BhTooltipHire fill (map object): BhTooltipHire.wae(fpq) : Void
    public static readonly global::System.Reflection.MethodInfo HireTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipHire), nameof(global::Hex.UI.Tooltips.BhTooltipHire.wae), typeof(GameDwelling));

    // intro videos step: ccj.qrx() : Void
    public static readonly global::System.Reflection.MethodInfo IntroVideos = M(typeof(GameIntroStep), nameof(GameIntroStep.qrx));

    // localized text: language changed: cpz.ihb() : Void
    public static readonly global::System.Reflection.MethodInfo LanguageChanged = M(typeof(GameLocalizedText), nameof(GameLocalizedText.ihb));

    // other forecast producers: none on this version
    public static readonly global::System.Reflection.MethodInfo[] OtherForecastMakers = global::System.Array.Empty<global::System.Reflection.MethodInfo>();

    // outline colors to the shader: bei.lkv(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.cfk(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void, bei.dhb(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void
    public static readonly global::System.Reflection.MethodInfo[] OutlineColorsToShader =
    {
        M(typeof(global::bei), nameof(global::bei.lkv), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.cfk), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
        M(typeof(global::bei), nameof(global::bei.dhb), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
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

    // BhSkulls fill for a party: BhSkulls.umo(DataParty) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForParty = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umo), typeof(global::Hex.Session.Data.DataParty));

    // BhSkulls fill for a squad: BhSkulls.umn(frc) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForSquad = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umn), typeof(GameSquad));

    // BhSkulls level (hero power, squad power): BhSkulls.ump(Int32, Int32) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsLevel = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.ump), typeof(int), typeof(int));

    // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.weh(ESquadTooltipType, Object) : Void
    public static readonly global::System.Reflection.MethodInfo SquadTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipSquad), nameof(global::Hex.UI.Tooltips.BhTooltipSquad.weh), typeof(global::Hex.UI.Tooltips.ESquadTooltipType), typeof(global::Il2CppSystem.Object));

    // positioner place for point: cxg.wdp(Vector2) : Vector2, cwn.wdp(Vector2) : Vector2, cwr.wdp(Vector2) : Vector2
    public static readonly global::System.Reflection.MethodInfo[] TooltipPlaceForPoint =
    {
        M(typeof(GameTooltipPositioner), nameof(GameTooltipPositioner.wdp), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwn), nameof(global::cwn.wdp), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwr), nameof(global::cwr.wdp), typeof(global::UnityEngine.Vector2)),
    };

    // tooltip set texts: BhTooltip.vzp(BasicTooltip, crv) : Void
    public static readonly global::System.Reflection.MethodInfo TooltipSetTexts = M(typeof(global::Hex.UI.Tooltips.BhTooltip), nameof(global::Hex.UI.Tooltips.BhTooltip.vzp), typeof(global::Hex.UI.BasicTooltip), typeof(GameTooltipOptions));
}
