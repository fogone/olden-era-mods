// Generated for the game 0.82.08 (tools/game-names from the game's files, or the research plugin's `aliases`). Do not edit:
// make it again (docs/game-names.md). The lookups behind each name are the Lookup.Generate ones in Game*.cs,
// the choices among several candidates are in GameNames.pins.txt. A name is public when it shows no obfuscated class
// (GameTypes.g.cs): it then stays the same on the next version of the game; the others are the library's own.

namespace Nobirds.OldenEra.Modding.Common;

public static class GameNames
{
    public const string GameVersion = "0.82.08";

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

    // always battle (static): lh.bvdt : Boolean
    public static bool AlwaysBattle => global::lh.bvdt;

    // forecast builder: exw.bjfe(DamageUiParams, Boolean) : exv
    internal static GameForecast BuildForecast(global::Hex.Session.Battle.DamageUiParams a0, bool a1) => global::exw.bjfe(a0, a1);

    // data roots: bkp.byxl : Il2CppReferenceArray`1
    internal static global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameDataRoot> DataRoots { get => global::bkp.byxl; set => global::bkp.byxl = value; }

    // encounter: dkw.zkh(fgd, frc) : Void
    internal static void Encounter(GameHero a0, GameSquad a1) => global::dkw.zkh(a0, a1);

    // HUD hero panel: BhHeroHUDPanel.me : BhHeroHUDPanel
    public static global::Hex.Session.Hud.BhHeroHUDPanel HudPanel => global::Hex.Session.Hud.BhHeroHUDPanel.me;

    // diplomacy check: ffd.blol(fgd, frc) : Boolean
    internal static bool JoinsByDiplomacy(GameHero a0, GameSquad a1) => global::ffd.blol(a0, a1);

    // diplomacy check explained: ffd.blok(fgd, frc, String&) : Boolean
    internal static bool JoinsByDiplomacyExplained(GameHero a0, GameSquad a1, out string a2) => global::ffd.blok(a0, a1, out a2);

    // join share: ffd.blom(frc, fgd) : Single
    internal static float JoinShare(GameSquad a0, GameHero a1) => global::ffd.blom(a0, a1);

    // localization service: iv.buvv : cav
    internal static GameLocalization Localization => global::iv.buvv;

    // map session: feh.me : feh
    internal static GameMapSession MapSession => global::feh.me;

    // resource manager instance: ResManager.bsbr() : ResManager
    public static global::Hex.ResManager ResourceManager() => global::Hex.ResManager.bsbr();

    // screen registry instance: cqp.ujd() : cqp
    internal static GameScreenRegistry ScreenRegistry() => global::cqp.ujd();

    // session: dkd.Me : dkd
    internal static GameSession Session => global::dkd.Me;

    // settings service: bak.bwsc : bak
    internal static GameSettingsService SettingsService => global::bak.bwsc;

    // sprite registry instance: coi.cdwo : coi
    internal static GameSpriteRegistry SpriteRegistry => global::coi.cdwo;

    // tooltips holder: BhTooltips.cftf : BhTooltips
    public static global::Hex.UI.Tooltips.BhTooltips Tooltips => global::Hex.UI.Tooltips.BhTooltips.cftf;

    // unit portraits: efr.bday(String) : Sprite
    public static global::UnityEngine.Sprite UnitPortrait(string a0) => global::efr.bday(a0);

    extension(GameAbilities abilities)
    {
        // ability container -> ability (EAbilityType): ejh.besz(EAbilityType) : eji
        internal GameAbility Ability(global::Hex.Configs.EAbilityType a0) => abilities.besz(a0);
    }

    extension(GameAbility ability)
    {
        // ability -> buff configs: eji.clyl.cvmi, eji.clyl.cvmj, eji.clyl.cvmk
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffConfig> BuffConfigs => GameNames.Each<global::Hex.Configs.BuffConfig>(() => ability.clyl?.cvmi, () => ability.clyl?.cvmj, () => ability.clyl?.cvmk);

        // ability -> cast configs: eji.beut().config, eji.beut().data, eji.beus().config, eji.beus().data, eji.clyh.config, eji.clyh.data, eji.clyi.config, eji.clyi.data, eji.clyj.config, eji.clyj.data, eji.clyl.damageDealer, eji.clyl.selfDealer, eji.clyl.allyDealer
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Configs.CastDealerConfig> CastConfigs => GameNames.Each<global::Hex.Configs.CastDealerConfig>(() => ability.beut()?.config, () => ability.beut()?.data, () => ability.beus()?.config, () => ability.beus()?.data, () => ability.clyh?.config, () => ability.clyh?.data, () => ability.clyi?.config, () => ability.clyi?.data, () => ability.clyj?.config, () => ability.clyj?.data, () => ability.clyl?.damageDealer, () => ability.clyl?.selfDealer, () => ability.clyl?.allyDealer);

        // ability -> AbilityLogicConfig: eji.clyl : AbilityLogicConfig
        internal global::Hex.Configs.AbilityLogicConfig LogicConfig => ability.clyl;
    }

    extension(GameBattleLogic battleLogic)
    {
        // battle logic: the defender answers before the strike: eqe.bhpu(Unit) : Boolean
        internal bool AnswersBeforeStrike(global::Hex.Session.Battle.Unit a0) => battleLogic.bhpu(a0);

        // battle logic: a second strike after the counter: eqe.bhpv(Unit, eji) : Boolean
        internal bool StrikesAgainAfterCounter(global::Hex.Session.Battle.Unit a0, GameAbility a1) => battleLogic.bhpv(a0, a1);
    }

    extension(GameBonus bonus)
    {
        // bonus -> receiver: dwa.cjes : dwc
        internal GameBonusReceiver Receiver => bonus.cjes;
    }

    extension(GameBonusReceiver bonusReceiver)
    {
        // bonus receiver add: dwc.bavh(dwa, Boolean) : Void
        internal void Add(GameBonus a0, bool a1) => bonusReceiver.bavh(a0, a1);
    }

    extension(GameBuffs buffs)
    {
        // buffs -> lists: elg.cmgb, elg.cmgc, elg.cmge
        internal global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.List<GameBuff>> Lists => GameNames.Each<global::Il2CppSystem.Collections.Generic.List<GameBuff>>(() => buffs.cmgb, () => buffs.cmgc, () => buffs.cmge);
    }

    extension(GameDataRoot dataRoot)
    {
        // data root enabled: bko.byxi : Boolean
        internal bool Enabled { get => dataRoot.byxi; set => dataRoot.byxi = value; }

        // data root folder: bko.byxk : String
        internal string Folder { get => dataRoot.byxk; set => dataRoot.byxk = value; }

        // data root name: bko.byxj : String
        internal string Name { get => dataRoot.byxj; set => dataRoot.byxj = value; }
    }

    extension(GameDwelling dwelling)
    {
        // dwelling -> bonuses: fpq.crii : List`1
        internal global::Il2CppSystem.Collections.Generic.List<GameBonus> Bonuses => dwelling.crii;
    }

    extension(GameEntity entity)
    {
        // meeting check: fhf.bmdo(fhf) : Boolean
        internal bool Meets(GameEntity a0) => entity.bmdo(a0);
    }

    extension(GameForecast forecast)
    {
        // forecast: damage max: exv.cozw : Int32
        internal int DamageMax { get => forecast.cozw; set => forecast.cozw = value; }

        // forecast: damage min: exv.cozv : Int32
        internal int DamageMin { get => forecast.cozv; set => forecast.cozv = value; }

        // forecast: killed max: exv.cpab : Int32
        internal int KilledMax { get => forecast.cpab; set => forecast.cpab = value; }

        // forecast: killed min: exv.cpaa : Int32
        internal int KilledMin { get => forecast.cpaa; set => forecast.cpaa = value; }
    }

    extension(GameHero hero)
    {
        // hero strength: fgd.blvy(Boolean) : Int32
        internal int ArmyStrength(bool a0) => hero.blvy(a0);

        // hero -> its data (Hex.Session.Data.Hero, with the party): fgd.cwtm : Hero
        internal global::Hex.Session.Data.Hero Data => hero.cwtm;

        // hero -> entity: fgd.cqes : fhf
        internal GameEntity Entity => hero.cqes;

        // hero -> HeroStat paths: fgd.cqej.cjqu, fgd.cqej.cjqv, fgd.cwtm.statsByLevel, fgd.cwtm.additionalStats, fgd.cwtn, fgd.cwto
        internal global::System.Collections.Generic.IEnumerable<global::Hex.Session.Data.HeroStat> HeroStats => GameNames.Each<global::Hex.Session.Data.HeroStat>(() => hero.cqej?.cjqu, () => hero.cqej?.cjqv, () => hero.cwtm?.statsByLevel, () => hero.cwtm?.additionalStats, () => hero.cwtn, () => hero.cwto);

        // hero -> Logic.Hero: fgd.cqej : Hero
        internal global::Hex.Session.Logic.Hero Logic => hero.cqej;

        // hero -> side wrapper: fgd.cqek : fqu
        internal GameSide SideOwner => hero.cqek;
    }

    extension(GameInputBoxOptions inputBoxOptions)
    {
        // input box cancel text: dck.chgc : String
        internal string CancelText { get => inputBoxOptions.chgc; set => inputBoxOptions.chgc = value; }

        // input box caption: dck.chfz : String
        internal string Caption { get => inputBoxOptions.chfz; set => inputBoxOptions.chfz = value; }

        // input box confirm text: dck.chgb : String
        internal string ConfirmText { get => inputBoxOptions.chgb; set => inputBoxOptions.chgb = value; }

        // input box on cancel: dck.chgh : Action
        internal global::Il2CppSystem.Action OnCancel { get => inputBoxOptions.chgh; set => inputBoxOptions.chgh = value; }

        // input box on confirm: dck.chgi : Action`1
        internal global::Il2CppSystem.Action<string> OnConfirm { get => inputBoxOptions.chgi; set => inputBoxOptions.chgi = value; }

        // input box placeholder: dck.chga : String
        internal string Placeholder { get => inputBoxOptions.chga; set => inputBoxOptions.chga = value; }
    }

    extension(GameInputReceiver inputReceiver)
    {
        // bind key: cbe.qpo(EInputAction, EInputState, Action) : Void
        internal void Bind(global::Hex.InputSys.EInputAction a0, global::Hex.InputSys.EInputState a1, global::Il2CppSystem.Action a2) => inputReceiver.qpo(a0, a1, a2);
    }

    extension(GameIntroStep introStep)
    {
        // intro videos step done: ccj.ccga : Boolean
        internal bool Done { get => introStep.ccga; set => introStep.ccga = value; }
    }

    extension(GameLocalization localization)
    {
        // text of a key: cav.qmk(String) : String
        internal string Translate(string a0) => localization.qmk(a0);
    }

    extension(GameMapGameSession mapGameSession)
    {
        // session -> map data: dcz.chmi : Data
        internal global::Hex.Session.Data.Data MapData => mapGameSession.chmi;
    }

    extension(GameMapObject mapObject)
    {
        // map object -> model: fql.crkp : bye
        internal GameModelHolder Model => mapObject.crkp;

        // map object -> DataObject: fql.crkq : DataObject
        internal global::Hex.Session.Data.DataObject ObjectData => mapObject.crkq;

        // map object -> owner side: fql.crks : fqu
        internal GameSide Owner => mapObject.crks;

        // map object -> party: fql.crkr : eco
        internal GameParty Party => mapObject.crkr;

        // map object visited by side: fql.bnox(fqu) : Boolean
        internal bool VisitedBy(GameSide a0) => mapObject.bnox(a0);
    }

    extension(GameMapObjectStore mapObjectStore)
    {
        // map objects by id: fqm.crlg : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<int, GameMapObject> ById => mapObjectStore.crlg;
    }

    extension(GameMapSession mapSession)
    {
        // map session -> map objects: feh.cpye : fqm
        internal GameMapObjectStore MapObjects => mapSession.cpye;

        // map session -> squads: feh.cpyg : fre
        internal GameSquadStore Squads => mapSession.cpyg;
    }

    extension(GameMessageBoxButton messageBoxButton)
    {
        // message box button click: dcm.chgw : Action
        internal global::Il2CppSystem.Action Click { get => messageBoxButton.chgw; set => messageBoxButton.chgw = value; }

        // message box button label: dcm.chgv : String
        internal string Label { get => messageBoxButton.chgv; set => messageBoxButton.chgv = value; }
    }

    extension(GameMessageBoxOptions messageBoxOptions)
    {
        // message box cancel button: dcl.chgq : dcm
        internal GameMessageBoxButton Cancel { get => messageBoxOptions.chgq; set => messageBoxOptions.chgq = value; }

        // message box caption: dcl.chgl : String
        internal string Caption { get => messageBoxOptions.chgl; set => messageBoxOptions.chgl = value; }

        // message box confirm button: dcl.chgp : dcm
        internal GameMessageBoxButton Confirm { get => messageBoxOptions.chgp; set => messageBoxOptions.chgp = value; }

        // message box text: dcl.chgm : String
        internal string Text { get => messageBoxOptions.chgm; set => messageBoxOptions.chgm = value; }
    }

    extension(GameModelHolder modelHolder)
    {
        // map object model -> GameObjects: bye._cbop_k__BackingField, bye.cbos, bye.cbop
        internal global::System.Collections.Generic.IEnumerable<global::UnityEngine.GameObject> SceneObjects => GameNames.Each<global::UnityEngine.GameObject>(() => modelHolder._cbop_k__BackingField, () => modelHolder.cbos, () => modelHolder.cbop);
    }

    extension(GameResourceEntry resourceEntry)
    {
        // resource entry: asset: gpu.ctyh : Object
        internal global::UnityEngine.Object Asset { get => resourceEntry.ctyh; set => resourceEntry.ctyh = value; }

        // resource entry: key: gpu.ctyg : gpt
        internal GameResourceKey Key { get => resourceEntry.ctyg; set => resourceEntry.ctyg = value; }
    }

    extension(GameResourceKey resourceKey)
    {
        // resource key: type: gpt.ctyf : Type
        internal global::Il2CppSystem.Type AssetType { get => resourceKey.ctyf; set => resourceKey.ctyf = value; }

        // resource key: bundle: gpt.ctye : String
        internal string Bundle { get => resourceKey.ctye; set => resourceKey.ctye = value; }

        // resource key: path: gpt.ctyd : String
        internal string Path { get => resourceKey.ctyd; set => resourceKey.ctyd = value; }
    }

    extension(GameSaveRowPresenter saveRowPresenter)
    {
        // save row on click: cum.cmv(Action) : Void
        internal void OnClick(global::Il2CppSystem.Action a0) => saveRowPresenter.cmv(a0);

        // save row name: cum.hav(String, String) : Void
        internal void ShowName(string a0, string a1) => saveRowPresenter.hav(a0, a1);
    }

    extension(GameScreenRegistry screenRegistry)
    {
        // hide screen by name: cqp.fwj(String) : Void
        internal void Hide(string a0) => screenRegistry.fwj(a0);

        // open screen by name: cqp.gwk(String) : BhScreen
        internal global::Hex.UI.BhScreen Open(string a0) => screenRegistry.gwk(a0);

        // open screen: cqp.ujk(BhScreen) : BhScreen
        internal global::Hex.UI.BhScreen OpenScreen(global::Hex.UI.BhScreen a0) => screenRegistry.ujk(a0);

        // open screens: cqp.ceoy : List`1
        internal global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.BhScreen> OpenScreens => screenRegistry.ceoy;

        // register screen: cqp.nie(BhScreen) : Void
        internal void Register(global::Hex.UI.BhScreen a0) => screenRegistry.nie(a0);

        // screen by name: cqp.ujh(String) : BhScreen
        internal global::Hex.UI.BhScreen Screen(string a0) => screenRegistry.ujh(a0);

        // registered screens: cqp.ceox : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<string, global::Hex.UI.BhScreen> Screens => screenRegistry.ceox;
    }

    extension(GameSession session)
    {
        // session -> how it is played (EPlay): dkd.cwbj : EPlay
        internal global::Hex.EPlay Play => session.cwbj;
    }

    extension(GameSettingsService settingsService)
    {
        // settings: bak.bwsd : SettingsData
        internal global::Hex.Settings.Data.SettingsData Settings => settingsService.bwsd;
    }

    extension(GameSide side)
    {
        // side -> AI: fqu.crmu : fzh
        internal GameSideAi Ai => side.crmu;

        // side is a person's: fqu.bnyk() : Boolean
        internal bool IsPerson() => side.cwye;

        // side -> logic with the bonus receiver: fqu.crlz : edc
        internal GameSideLogic Logic => side.crlz;

        // side wrapper -> Side: fqu.cwxm : Side
        internal global::Hex.Session.Data.Side Side => side.cwxm;

        // side -> state: fqu.crmi : fmr
        internal GameSideState State => side.crmi;
    }

    extension(GameSideLogic sideLogic)
    {
        // side logic -> bonus receiver: edc.cjyr : dyj
        internal GameSideBonusReceiver BonusReceiver => sideLogic.cjyr;
    }

    extension(GameSideState sideState)
    {
        // side state: no flee offers: fmr.cqyi : Boolean
        internal bool NoFleeOffers => sideState.cqyi;
    }

    extension(GameSimpleTabData simpleTabData)
    {
        // simple tab click: dcs.chiy : Action
        internal global::Il2CppSystem.Action Click { get => simpleTabData.chiy; set => simpleTabData.chiy = value; }

        // simple tab label: dcs.chix : String
        internal string Label { get => simpleTabData.chix; set => simpleTabData.chix = value; }
    }

    extension(GameSpriteRegistry spriteRegistry)
    {
        // sprite tables: coi.cdwp, coi.cdwq, coi.cdwr, coi.cdws, coi.cdwt, coi.cdwu, coi.cdwv, coi.cdww, coi.cdwx, coi.cdwy, coi.cdwz, coi.cdxa, coi.cdxb, coi.cdxc, coi.cdxd, coi.cdxe, coi.cdxf, coi.cdxg, coi.cdxh, coi.cdxi, coi.cdxj, coi.cdxk, coi.cdxl, coi.cdxm, coi.cdxn, coi.cdxo, coi.cdxp, coi.cdxq, coi.cdxr, coi.cdxs, coi.cdxt, coi.cdxu, coi.cdxv, coi.cdxw, coi.cdxx, coi.cdxy, coi.cdxz, coi.cdya, coi.cdyb, coi.cdyc, coi.cdyd
        internal global::System.Collections.Generic.IEnumerable<global::Il2CppSystem.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>> Tables => GameNames.Each<global::Il2CppSystem.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>>(() => spriteRegistry.cdwp, () => spriteRegistry.cdwq, () => spriteRegistry.cdwr, () => spriteRegistry.cdws, () => spriteRegistry.cdwt, () => spriteRegistry.cdwu, () => spriteRegistry.cdwv, () => spriteRegistry.cdww, () => spriteRegistry.cdwx, () => spriteRegistry.cdwy, () => spriteRegistry.cdwz, () => spriteRegistry.cdxa, () => spriteRegistry.cdxb, () => spriteRegistry.cdxc, () => spriteRegistry.cdxd, () => spriteRegistry.cdxe, () => spriteRegistry.cdxf, () => spriteRegistry.cdxg, () => spriteRegistry.cdxh, () => spriteRegistry.cdxi, () => spriteRegistry.cdxj, () => spriteRegistry.cdxk, () => spriteRegistry.cdxl, () => spriteRegistry.cdxm, () => spriteRegistry.cdxn, () => spriteRegistry.cdxo, () => spriteRegistry.cdxp, () => spriteRegistry.cdxq, () => spriteRegistry.cdxr, () => spriteRegistry.cdxs, () => spriteRegistry.cdxt, () => spriteRegistry.cdxu, () => spriteRegistry.cdxv, () => spriteRegistry.cdxw, () => spriteRegistry.cdxx, () => spriteRegistry.cdxy, () => spriteRegistry.cdxz, () => spriteRegistry.cdya, () => spriteRegistry.cdyb, () => spriteRegistry.cdyc, () => spriteRegistry.cdyd);
    }

    extension(GameSquad squad)
    {
        // squad -> entity: frc.cron : fhf
        internal GameEntity Entity => squad.cron;

        // squad -> DataSquad: frc.croc : DataSquad
        internal global::Hex.Session.Data.DataSquad SquadData => squad.croc;

        // squad -> hero-like owners: frc.cror, frc.cwyk
        internal global::System.Collections.Generic.IEnumerable<GameHero> SquadOwners => GameNames.Each<GameHero>(() => squad.cror, () => squad.cwyk);

        // squad strength: frc.boef() : Int32
        internal int SquadStrength() => squad.boef();

        // squad -> view: frc.croi : fno
        internal GameSquadView View => squad.croi;
    }

    extension(GameSquadEntity squadEntity)
    {
        // squad entity -> squad: fgy.cqhf : frc
        internal GameSquad Squad => squadEntity.cqhf;
    }

    extension(GameSquadStore squadStore)
    {
        // squads by id: fre.croy : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<int, GameSquad> ById => squadStore.croy;
    }

    extension(GameSquadView squadView)
    {
        // squad view -> GameObject: fno.crck : GameObject
        internal global::UnityEngine.GameObject Model => squadView.crck;
    }

    extension(GameUnitBase unitBase)
    {
        // Unit -> ability container: eon.bgxn() : ejh
        internal GameAbilities Abilities() => unitBase.cnco;

        // Unit -> buffs: eon.cwnw : elg
        internal GameBuffs Buffs => unitBase.cwnw;
    }

    extension(global::Hex.Configs.UnitPassiveConfig it)
    {
        // passive -> buff data: UnitPassiveConfig.data
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.BuffData> BuffDatas => GameNames.Each<global::Hex.Configs.BuffData>(() => it.data);
    }

    extension(global::Hex.GameHub.UICommon.BhTab it)
    {
        // hub tab click: BhTab.ccmg : Action`1
        internal global::Il2CppSystem.Action<GameTabItem> Click { get => it.ccmg; set => it.ccmg = value; }

        // hub tab available: BhTab.jis(Boolean) : Void
        public void SetAvailable(bool a0) => it.jis(a0);

        // hub tab selected look: BhTab.jit(Boolean) : Void
        public void ShowSelected(bool a0) => it.jit(a0);
    }

    extension(global::Hex.MainMenu.MainMenuNavigationView it)
    {
        // main menu open sub-menu: MainMenuNavigationView.ccbq : MainMenuSubNavView
        public global::Hex.MainMenu.MainMenuSubNavView OpenSubMenu => it.ccbq;

        // main menu select: MainMenuNavigationView.qjp(cab) : Boolean
        internal bool Select(GameMenuSelectable a0) => it.qjp(a0);
    }

    extension(global::Hex.ResManager it)
    {
        // resource table: ResManager.ctyt : Dictionary`2
        internal global::Il2CppSystem.Collections.Generic.Dictionary<GameResourceKey, GameResourceEntry> Assets => it.ctyt;
    }

    extension(global::Hex.Session.Battle.Unit it)
    {
        // Unit -> UnitData: Unit.cpgx : UnitData
        public global::Hex.Session.Battle.UnitData Data => it.cpgx;

        // unit: has a sequence effect: Unit.bjnu(String) : Boolean
        public bool HasSequenceEffect(string a0) => it.bjnu(a0);

        // Unit -> UnitLogicConfig: Unit.cpgv : UnitLogicConfig
        public global::Hex.Configs.UnitLogicConfig LogicConfig => it.cpgv;
    }

    extension(global::Hex.Session.Battle.UnitData it)
    {
        // UnitData: top creature's health: UnitData.cpmf : Int32
        public int TopHealth { get => it.cpmf; set => it.cpmf = value; }
    }

    extension(global::Hex.Session.Data.DataFogOfWar it)
    {
        // fog: cell hidden: DataFogOfWar.bkii(Int32) : Boolean
        public bool InFog(int a0) => it.bkii(a0);
    }

    extension(global::Hex.Session.Data.DataObject it)
    {
        // map object data -> configs: DataObject.cpuv, DataObject.cwsq
        public global::System.Collections.Generic.IEnumerable<global::Hex.Configs.ObjConfigBase> Configs => GameNames.Each<global::Hex.Configs.ObjConfigBase>(() => it.cpuv, () => it.cwsq);
    }

    extension(global::Hex.Session.Data.Hero it)
    {
        // party wrapper -> DataParty: Hero.party : DataParty
        public global::Hex.Session.Data.DataParty Army => it.party;
    }

    extension(global::Hex.Session.Hud.BhHeroHUDPanel it)
    {
        // HUD hero panel -> hero: BhHeroHUDPanel.ciku : fgd
        internal GameHero Hero => it.ciku;
    }

    extension(global::Hex.Session.Logic.Hero it)
    {
        // hero visit id: Hero.cwfb : Int32
        public int VisitId => it.cwfb;
    }

    extension(global::Hex.Settings.UI.BhSettingsCheckbox it)
    {
        // switch callback: BhSettingsCheckbox.bwqo : Action`1
        public global::Il2CppSystem.Action<bool> SwitchCallback { get => it.bwqo; set => it.bwqo = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsSection it)
    {
        // section type: BhSettingsSection.bwqs : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionType => it.bwqs;

        // section type field: BhSettingsSection._bwqs_k__BackingField : ESettingsType
        public global::Hex.Settings.UI.ESettingsType SectionTypeStored { get => it._bwqs_k__BackingField; set => it._bwqs_k__BackingField = value; }
    }

    extension(global::Hex.Settings.UI.BhSettingsTab it)
    {
        // tab click action: BhSettingsTab.bwqy : Action`1
        public global::Il2CppSystem.Action<global::Hex.Settings.UI.ESettingsType> ClickAction { get => it.bwqy; set => it.bwqy = value; }

        // tab selected look: BhSettingsTab.kqd(Boolean) : Void
        public void ShowSelected(bool a0) => it.kqd(a0);
    }

    extension(global::Hex.Settings.UI.SettingsSectionsView it)
    {
        // shown section: SettingsSectionsView.bwru : BhSettingsSection
        public global::Hex.Settings.UI.BhSettingsSection ShownSection => it.bwru;

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
        // button available: BhGenericButton.uha(Boolean) : Void
        public void SetAvailable(bool a0) => it.uha(a0);
    }

    extension(global::Hex.UI.BhScreen it)
    {
        // screen hidden at once: BhScreen.udm() : Void
        public void HideAtOnce() => it.udm();

        // screen input: BhScreen.cekr : cbe
        internal GameInputReceiver Input => it.cekr;

        // screen is open: BhScreen.ceko : Boolean
        public bool IsOpen => it.ceko;

        // screen one-time init: BhScreen.hbq() : Void
        public void SetUpOnce() => it.hbq();

        // screen when closed: BhScreen.cekj : Action
        public global::Il2CppSystem.Action WhenClosed { get => it.cekj; set => it.cekj = value; }
    }

    extension(global::Hex.UI.BhSkulls it)
    {
        // BhSkulls level colour: BhSkulls.umn(Int32) : Color
        public global::UnityEngine.Color DangerColor(int a0) => it.umn(a0);

        // BhSkulls level (hero power, squad power): BhSkulls.umm(Int32, Int32) : Int32
        public int DangerLevel(int a0, int a1) => it.umm(a0, a1);

        // BhSkulls fill for a party: BhSkulls.uml(DataParty) : Int32
        public int ShowDangerOfParty(global::Hex.Session.Data.DataParty a0) => it.uml(a0);

        // BhSkulls fill for a squad: BhSkulls.umk(frc) : Int32
        internal int ShowDangerOfSquad(GameSquad a0) => it.umk(a0);
    }

    extension(global::Hex.UI.BhTooltipGarnison it)
    {
        // BhTooltipGarnison fill (party, bool): BhTooltipGarnison.ung(eco, Boolean) : Void
        internal void FillWith(GameParty a0, bool a1) => it.ung(a0, a1);
    }

    extension(global::Hex.UI.GeneralKit.BhCheckbox it)
    {
        // switch is on: BhCheckbox.xyw() : Boolean
        public bool SwitchedOn() => it.xyw();
    }

    extension(global::Hex.UI.GeneralKit.BhNavigationButton it)
    {
        // menu button click: BhNavigationButton.chbk : Action
        public global::Il2CppSystem.Action MenuClick { get => it.chbk; set => it.chbk = value; }
    }

    extension(global::Hex.UI.GeneralKit.BhTag it)
    {
        // tag chip colour: BhTag.yep(Color) : Void
        public void ShowColor(global::UnityEngine.Color a0) => it.yep(a0);

        // tag chip text: BhTag.yen(String) : Void
        public void ShowText(string a0) => it.yen(a0);
    }

    extension(global::Hex.UI.GeneralKit.BhToggle it)
    {
        // check mark show value: BhToggle.yer(Boolean) : Void
        public void ShowValue(bool a0) => it.yer(a0);
    }

    extension(global::Hex.UI.SaveLoad.BhFilter it)
    {
        // save filter elements: BhFilter.cfav : List`1
        public global::Il2CppSystem.Collections.Generic.List<global::Hex.UI.SaveLoad.BhFilterElement> Elements => it.cfav;

        // save filter set up: BhFilter.uxf(Il2CppStringArray, Action) : Void
        public void SetUp(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray a0, global::Il2CppSystem.Action a1) => it.uxf(a0, a1);
    }

    extension(global::Hex.UI.SaveLoad.BhFilterElement it)
    {
        // save filter element ticked: BhFilterElement.uxl() : Boolean
        public bool Ticked() => it.uxl();
    }

    extension(global::Hex.UI.SaveLoad.BhSessionInfoElement it)
    {
        // save row presenter: BhSessionInfoElement.vez() : cum
        internal GameSaveRowPresenter Presenter() => it.vez();

        // save row campaign shown: BhSessionInfoElement.vfh(Boolean) : Void
        public void ShowCampaign(bool a0) => it.vfh(a0);

        // save row icon: BhSessionInfoElement.vfc(Sprite) : Void
        public void ShowIcon(global::UnityEngine.Sprite a0) => it.vfc(a0);

        // save row cloud: BhSessionInfoElement.vfb(Boolean) : Void
        public void ShowInCloud(bool a0) => it.vfb(a0);

        // save row map icons shown: BhSessionInfoElement.vfk(Boolean) : Void
        public void ShowMapIcons(bool a0) => it.vfk(a0);

        // save row selected look: BhSessionInfoElement.vfm(Boolean) : Void
        public void ShowSelected(bool a0) => it.vfm(a0);

        // save row tag: BhSessionInfoElement.vfe(Int32, String, Color) : Void
        public void ShowTag(int a0, string a1, global::UnityEngine.Color a2) => it.vfe(a0, a1, a2);

        // save row tag shown: BhSessionInfoElement.vff(Int32, Boolean) : Void
        public void ShowTagShown(int a0, bool a1) => it.vff(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltip it)
    {
        // tooltip -> positioner: BhTooltip.cfsd : cxg
        internal GameTooltipPositioner Positioner => it.cfsd;

        // tooltip set texts: BhTooltip.vzl(BasicTooltip, crv) : Void
        internal void SetTexts(global::Hex.UI.BasicTooltip a0, GameTooltipOptions a1) => it.vzl(a0, a1);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipHire it)
    {
        // BhTooltipHire fill (map object): BhTooltipHire.waa(fpq) : Void
        internal void FillFor(GameDwelling a0) => it.waa(a0);
    }

    extension(global::Hex.UI.Tooltips.BhTooltips it)
    {
        // text tooltip: BhTooltips.wbr(String, String, String, RectTransform, crv, ESnapDirection, Boolean) : Void
        internal void ShowTexts(string a0, string a1, string a2, global::UnityEngine.RectTransform a3, GameTooltipOptions a4, global::Hex.UI.Tooltips.ESnapDirection a5, bool a6) => it.wbr(a0, a1, a2, a3, a4, a5, a6);
    }

    extension(global::Hex.UI.Tooltips.BhTooltipSquad it)
    {
        // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.weg(ESquadTooltipType, Object) : Void
        public void FillFor(global::Hex.UI.Tooltips.ESquadTooltipType a0, global::Il2CppSystem.Object a1) => it.weg(a0, a1);
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

    // counter decision: eqe.bhqe(Unit, Unit, eji, Hex, List`1) : Boolean
    public static readonly global::System.Reflection.MethodInfo CounterDecision = M(typeof(GameBattleLogic), nameof(GameBattleLogic.bhqe), typeof(global::Hex.Session.Battle.Unit), typeof(global::Hex.Session.Battle.Unit), typeof(GameAbility), typeof(global::Hex.Session.Battle.Hex), typeof(global::Il2CppSystem.Collections.Generic.List<global::Hex.Session.Battle.Hex>));

    // data roots builders: bkp.htm() : Void, bkp.nbv() : Void
    public static readonly global::System.Reflection.MethodInfo[] DataRootsBuilders =
    {
        M(typeof(global::bkp), nameof(global::bkp.htm)),
        M(typeof(global::bkp), nameof(global::bkp.nbv)),
    };

    // diplomacy points (squad, hero): ffd.blop(fgd, frc, DiplomacyInfoConfig) : Int32, ffd.blor(fgd, frc, DiplomacyInfoConfig) : Int32
    public static readonly global::System.Reflection.MethodInfo[] DiplomacyPoints =
    {
        M(typeof(global::ffd), nameof(global::ffd.blop), typeof(GameHero), typeof(GameSquad), typeof(global::Hex.Configs.DiplomacyInfoConfig)),
        M(typeof(global::ffd), nameof(global::ffd.blor), typeof(GameHero), typeof(GameSquad), typeof(global::Hex.Configs.DiplomacyInfoConfig)),
    };

    // encounter: dkw.zkh(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo Encounter = M(typeof(global::dkw), nameof(global::dkw.zkh), typeof(GameHero), typeof(GameSquad));

    // encounter actions: fhu.bmem(ELockType, fhf, fhf) : Void, efh.bcum(frc, fgd) : Void, efh.bcvb(frc, fgd) : Void, dkw.zkg(frc, fgd) : Void, dkw.zki(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo[] EncounterActions =
    {
        M(typeof(global::fhu), nameof(global::fhu.bmem), typeof(global::Hex.Session.World.ELockType), typeof(GameEntity), typeof(GameEntity)),
        M(typeof(global::efh), nameof(global::efh.bcum), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::efh), nameof(global::efh.bcvb), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zkg), typeof(GameSquad), typeof(GameHero)),
        M(typeof(global::dkw), nameof(global::dkw.zki), typeof(GameHero), typeof(GameSquad)),
    };

    // encounter battle: dkw.zki(fgd, frc) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterBattle = M(typeof(global::dkw), nameof(global::dkw.zki), typeof(GameHero), typeof(GameSquad));

    // encounter: joins at once: dkw.zkg(frc, fgd) : Void
    public static readonly global::System.Reflection.MethodInfo EncounterJoinAtOnce = M(typeof(global::dkw), nameof(global::dkw.zkg), typeof(GameSquad), typeof(GameHero));

    // forecast builders: exw.bjfe(DamageUiParams, Boolean) : exv, exw.iem(DamageUiParams, Boolean) : exv, exw.ibm(DamageUiParams, Boolean) : exv, exw.bgs(DamageUiParams, Boolean) : exv
    public static readonly global::System.Reflection.MethodInfo[] ForecastBuilders =
    {
        M(typeof(global::exw), nameof(global::exw.bjfe), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.iem), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.ibm), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
        M(typeof(global::exw), nameof(global::exw.bgs), typeof(global::Hex.Session.Battle.DamageUiParams), typeof(bool)),
    };

    // BhTooltipHire fill (map object): BhTooltipHire.waa(fpq) : Void
    public static readonly global::System.Reflection.MethodInfo HireTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipHire), nameof(global::Hex.UI.Tooltips.BhTooltipHire.waa), typeof(GameDwelling));

    // intro videos step: ccj.qrx() : Void
    public static readonly global::System.Reflection.MethodInfo IntroVideos = M(typeof(GameIntroStep), nameof(GameIntroStep.qrx));

    // localized text: language changed: cpz.ihb() : Void
    public static readonly global::System.Reflection.MethodInfo LanguageChanged = M(typeof(GameLocalizedText), nameof(GameLocalizedText.ihb));

    // other forecast producers: none on this version
    public static readonly global::System.Reflection.MethodInfo[] OtherForecastMakers = global::System.Array.Empty<global::System.Reflection.MethodInfo>();

    // outline colors to the shader: bei.lkr(CommandBuffer, Il2CppStructArray`1, Il2CppStructArray`1) : Void
    public static readonly global::System.Reflection.MethodInfo[] OutlineColorsToShader =
    {
        M(typeof(global::bei), nameof(global::bei.lkr), typeof(global::UnityEngine.Rendering.CommandBuffer), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(global::Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<global::UnityEngine.Color>)),
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

    // BhSkulls fill for a party: BhSkulls.uml(DataParty) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForParty = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.uml), typeof(global::Hex.Session.Data.DataParty));

    // BhSkulls fill for a squad: BhSkulls.umk(frc) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsForSquad = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umk), typeof(GameSquad));

    // BhSkulls level (hero power, squad power): BhSkulls.umm(Int32, Int32) : Int32
    public static readonly global::System.Reflection.MethodInfo SkullsLevel = M(typeof(global::Hex.UI.BhSkulls), nameof(global::Hex.UI.BhSkulls.umm), typeof(int), typeof(int));

    // BhTooltipSquad fill (ESquadTooltipType, object): BhTooltipSquad.weg(ESquadTooltipType, Object) : Void
    public static readonly global::System.Reflection.MethodInfo SquadTooltipFill = M(typeof(global::Hex.UI.Tooltips.BhTooltipSquad), nameof(global::Hex.UI.Tooltips.BhTooltipSquad.weg), typeof(global::Hex.UI.Tooltips.ESquadTooltipType), typeof(global::Il2CppSystem.Object));

    // positioner place for point: cxg.wdo(Vector2) : Vector2, cwn.wdo(Vector2) : Vector2, cwr.wdo(Vector2) : Vector2
    public static readonly global::System.Reflection.MethodInfo[] TooltipPlaceForPoint =
    {
        M(typeof(GameTooltipPositioner), nameof(GameTooltipPositioner.wdo), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwn), nameof(global::cwn.wdo), typeof(global::UnityEngine.Vector2)),
        M(typeof(global::cwr), nameof(global::cwr.wdo), typeof(global::UnityEngine.Vector2)),
    };

    // tooltip set texts: BhTooltip.vzl(BasicTooltip, crv) : Void
    public static readonly global::System.Reflection.MethodInfo TooltipSetTexts = M(typeof(global::Hex.UI.Tooltips.BhTooltip), nameof(global::Hex.UI.Tooltips.BhTooltip.vzl), typeof(global::Hex.UI.BasicTooltip), typeof(GameTooltipOptions));
}
