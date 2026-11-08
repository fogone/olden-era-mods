# Bug report: dwelling growth bonus lost after loading a save

For the game's in-game bug report form (it attaches the current save and the logs). Game 0.82.07.

**Title:** Weekly growth bonus from captured map dwellings is lost after loading a save

**Category:** Gameplay

**Description:**

Unit dwellings on the adventure map add weekly growth to their unit in the owner's towns ("cityUnitsIncrementFromBarrack",
e.g. Menhirs: root iriads +2, the faun's dwelling: elf trackers +4). The bonus works right after the capture, but after
saving and loading the game it is gone for every dwelling captured before the save, for all players (AI too). A dwelling
captured after the load works again until the next load.

Steps to reproduce:
1. Capture a unit dwelling that gives town growth (e.g. Menhirs, barracks_nature_3), with a town of that faction.
2. Open the town's hire window: the unit's weekly growth includes the dwelling's +2.
3. Save, load the save.
4. Open the same hire window: the growth is 2 lower. The dwelling still belongs to you.

Expected: the dwelling's growth bonus stays after loading.

What we found (looking at the running game, 0.82.07, obfuscated names as in this build):
- The dwelling's bonus object (dys, CityUnitsIncrementFromBarrack) still exists after the load, in the dwelling's bonus
  list (fpq.crih), but it has no receiver (its receiver field is null and it is not active) and it is not in the side's
  bonus receiver's list for that type. The hire window counts only the bonuses in that list.
- On capture, the dwelling's change of owner (fpq.bnox(newSide, oldSide)) takes the bonuses off the old side's receiver
  and adds them to the new side's (receiver.bavk(bonus, false)). On load, the dwelling's bonuses are created anew
  (fpq.Init) and the owner is restored without that step, so the bonuses are never added to the owner's receiver.
- Adding them to the owner's receiver after the load (the same call the capture makes) brings the growth back.

---

Russian summary for fogone: после загрузки бонус прироста от захваченных жилищ существует, но не подключён к приёмнику
бонусов стороны-владельца: при захвате игра его подключает (`fpq.bnox`), при загрузке — нет. Касается всех сторон.
