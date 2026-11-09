# Nobirds.OldenEra.Modding.Sdk

An MSBuild SDK for mods of Heroes of Might and Magic: Olden Era on BepInEx 6 (IL2CPP). Not affiliated with the game's
publishers.

```xml
<Project Sdk="Nobirds.OldenEra.Modding.Sdk/0.1.1">
  <PropertyGroup>
    <AssemblyName>MyMod</AssemblyName>
    <RootNamespace>MyName.MyMod</RootNamespace>
    <ModId>myname.oldenera.mymod</ModId>
    <ModName>My Mod</ModName>
    <Version>0.1.0</Version>
    <ModTags>ui;map</ModTags>
    <ModRequires>nobirds.oldenera.core</ModRequires>
    <ModAuthor>myname</ModAuthor>
    <ModSource>https://github.com/myname/my-mods</ModSource>
  </PropertyGroup>
</Project>
```

```csharp
[BepInPlugin(ModInfo.Id, ModInfo.Name, ModInfo.Version)]
public class Plugin : ModBase { protected override void OnLoad() { } }
```

- **The game is needed to build**: its .NET runtime (`<game>\dotnet`) and BepInEx's wrappers of its code
  (`<game>\BepInEx\interop`, made when the game starts with BepInEx). The SDK finds a Game Pass install; elsewhere set
  `OLDENERA_GAME` to the folder with `HeroesOldenEra.exe`.
- **Texts** go to `lang\<language>.json` (`english`, `russian`, ...): `{"sid": "<key>", "text": "<text>"}` entries. The
  mod's name and description in every language: the keys `oe.<last part of ModId>.title` and `.description`.
- `dotnet build -t:ModPackage` makes `bin\<configuration>\<ModId>-<Version>.zip`: `manifest.json` (what the mod is, its
  names and descriptions, the game version it was built on), `plugins\<the DLL>`, and `README.md`, `icon.png`,
  `screenshots\` when the project has them.
- The library (`Nobirds.OldenEra.Modding.Common`: `ModBase`, `ModMenu`, `Texts`, the game's events, UI helpers) is
  installed in the game by the mod manager; the package carries a copy to compile against.

Start from the template: https://github.com/fogone/olden-era-mods-template (two example mods, a catalog of your own).
The framework: https://github.com/fogone/olden-era-mods/blob/main/docs/writing-a-mod.md
