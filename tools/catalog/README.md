# Nobirds.OldenEra.Modding.Catalog

`oe-catalog` writes a mod catalog for the Olden Era mod manager (Heroes of Might and Magic: Olden Era, BepInEx 6): the
root file `catalog.json` and one meta file per mod, `mods/<id>.json`. The manager reads them from any https address,
for example a GitHub repository's raw files. Not affiliated with the game's publishers.

```
dotnet tool install --global Nobirds.OldenEra.Modding.Catalog

oe-catalog add --catalog catalog --root-url https://raw.githubusercontent.com/<owner>/<repo>/main/catalog \
    --name "My mods" --package bin/Release/net6.0/<id>-<version>.zip=<the zip's download URL>
```

- `--package` takes the zip that `dotnet build -t:ModPackage` of `Nobirds.OldenEra.Modding.Sdk` made: its
  `manifest.json` gives the mod's id, version, game version, requirements, names, descriptions, tags, author and source;
  the sha256 is computed. The release is added to the mod's meta file.
- `--name` names the catalog (required the first time), `--icon <png>=<https url>` gives it an icon (512 KB at most).
- `--screenshot <png>=<https url>[|<caption>]` (repeated) replaces the mod's screenshots (2 MB each at most).
- A version is published once: the same version again with the same zip only adds the game version; another zip is
  refused (raise the mod's version).
- `oe-catalog meta --catalog <dir> --root-url <url> --id <id> [--tags a,b] [--screenshot ...]` changes only a mod's
  description.

The format: https://github.com/fogone/olden-era-mods/blob/main/docs/catalog.md
