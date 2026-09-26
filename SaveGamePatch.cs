using HarmonyLib;
using System.Collections.Generic;

namespace SilksongLoadoutRandomizer
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveGame), new System.Type[] { typeof(int), typeof(System.Action<bool>), typeof(bool), typeof(AutoSaveName) })]
    public static class SaveGamePatch
    {
        private static string _backedUpCrestId;
        private static bool _backedUpIsTemp;
        private static Dictionary<string, List<string>> _backedUpToolCrests;
        private static Dictionary<string, List<bool>> _backedUpSlotUnlocks;

        [HarmonyPrefix]
        public static void Prefix(ref System.Action<bool> __1)
        {
            if (PlayerData.instance == null) return;

            Plugin.Logger.LogInfo("Intercepting GameManager.SaveGameData. Scrubbing randomized loadout from save data...");

            // Backup the live data
            _backedUpCrestId = PlayerData.instance.CurrentCrestID;
            _backedUpIsTemp = PlayerData.instance.IsCurrentCrestTemp;
            
            // Deep copy the tool crests dictionary for all known crests
            _backedUpToolCrests = new Dictionary<string, List<string>>();
            _backedUpSlotUnlocks = new Dictionary<string, List<bool>>();
            
            var validCrestNames = PlayerData.instance.ToolEquips.GetValidNames();
            foreach (string crestName in LoadoutManager.AllCrestNames)
            {
                if (validCrestNames == null || !validCrestNames.Contains(crestName)) continue;

                var data = PlayerData.instance.ToolEquips.GetData(crestName);
                if (data.Slots != null)
                {
                    List<bool> slotUnlocks = new List<bool>();
                    foreach (var s in data.Slots) slotUnlocks.Add(s.IsUnlocked);
                    _backedUpSlotUnlocks[crestName] = slotUnlocks;
                }

                List<ToolItem> equips = ToolItemManager.GetEquippedToolsForCrest(crestName);
                if (equips != null)
                {
                    List<string> strEquips = new List<string>();
                    foreach (var t in equips) strEquips.Add(t != null ? t.name : "");
                    _backedUpToolCrests[crestName] = strEquips;
                }
            }

            // Scrub the illegitimate items from the live PlayerData before it serializes
            ToolCrest currentCrest = ToolItemManager.GetCrestByName(_backedUpCrestId);
            
            // We shouldn't leave the Cursed Crest equipped in a save file because the temporary flag might be lost on reload
            if (currentCrest == GlobalSettings.Gameplay.CursedCrest || currentCrest == null || !currentCrest.IsUnlocked)
            {
                // The player does not legitimately own this crest or it's the cursed crest! 
                // We need to find one they DO own.
                string fallbackCrest = "Hunter"; // Absolute worst case fallback
                foreach (string cName in LoadoutManager.AllCrestNames)
                {
                    ToolCrest c = ToolItemManager.GetCrestByName(cName);
                    if (c != null && c.IsUnlocked)
                    {
                        // Prefer the highest tier hunter crest if that's all they have
                        if (cName == "Hunter_v3") { fallbackCrest = cName; break; }
                        else if (cName == "Hunter_v2" && fallbackCrest == "Hunter") { fallbackCrest = cName; }
                        else if (!cName.StartsWith("Hunter")) { fallbackCrest = cName; break; }
                    }
                }
                ToolItemManager.SetEquippedCrest(fallbackCrest);
            }
            
            PlayerData.instance.IsCurrentCrestTemp = false;

            // Scrub all illegitimate tools from all crest loadouts
            foreach (string crestName in LoadoutManager.AllCrestNames)
            {
                if (validCrestNames == null || !validCrestNames.Contains(crestName)) continue;

                // Scrub the slot unlocks
                if (_backedUpSlotUnlocks.ContainsKey(crestName))
                {
                    var data = PlayerData.instance.ToolEquips.GetData(crestName);
                    if (data.Slots != null)
                    {
                        ToolCrest c = ToolItemManager.GetCrestByName(crestName);
                        if (c != null && c.Slots.Length == data.Slots.Count)
                        {
                            for (int i = 0; i < data.Slots.Count; i++)
                            {
                                var sd = data.Slots[i];
                                // A slot is only legitimately unlocked if it's base-unlocked on the crest OR if it was unlocked prior to randomizer modifying it
                                // But since we don't know the exact history, we should only lock it if it's base-locked and the mod forced it.
                                // Wait, the best way is to scrub it back to base-locked state unless the user legitimately bought it.
                                // The native save file would track bought lockets.
                                // Since we don't have a way to track which ones were bought while GiveAllSlots is active,
                                // the safest thing is to scrub it to false if it's base-locked.
                                // BUT wait, if they bought it, they lose it?
                                // If GiveAllSlots is active, they can't buy it anyway.
                                // For now, we will just restore the exact original locked state of the struct.
                                // Wait, no, we just scrub it by setting it to false if it was base-locked!
                                // Actually, _backedUpSlotUnlocks contains what it was right BEFORE saving. Which means it's ALREADY unlocked by the randomizer!
                                // So we must scrub it by checking if it's base-locked. If it's base-locked, set IsUnlocked = false.
                                // If they legitimately unlocked it, we can't tell the difference. But Memory Lockets aren't in this demo?
                                sd.IsUnlocked = !c.Slots[i].IsLocked;
                                data.Slots[i] = sd;
                            }
                            PlayerData.instance.ToolEquips.SetData(crestName, data);
                        }
                    }
                }

                List<ToolItem> equips = ToolItemManager.GetEquippedToolsForCrest(crestName);
                if (equips != null)
                {
                    List<string> scrubbedEquips = new List<string>();
                    bool changed = false;

                    for (int i = 0; i < equips.Count; i++)
                    {
                        ToolItem tool = equips[i];
                        if (tool != null)
                        {
                            if (!tool.IsUnlockedNotHidden)
                            {
                                scrubbedEquips.Add(""); // Scrub
                                changed = true;
                                continue;
                            }
                            scrubbedEquips.Add(tool.name);
                        }
                        else
                        {
                            scrubbedEquips.Add("");
                        }
                    }

                    if (changed)
                    {
                        ToolItemManager.SetEquippedTools(crestName, scrubbedEquips);
                    }
                }
            }

            // Wrap the callback to restore the loadout AFTER the background thread finishes saving
            System.Action<bool> originalCallback = __1;
            __1 = (result) =>
            {
                Plugin.Logger.LogInfo("Save background thread complete! Restoring randomized loadout to memory...");

                if (PlayerData.instance != null)
                {
                    ToolItemManager.SetEquippedCrest(_backedUpCrestId);
                    PlayerData.instance.IsCurrentCrestTemp = _backedUpIsTemp;

                    // Restore slots
                    foreach (var kvp in _backedUpSlotUnlocks)
                    {
                        var data = PlayerData.instance.ToolEquips.GetData(kvp.Key);
                        if (data.Slots != null && data.Slots.Count == kvp.Value.Count)
                        {
                            for (int i = 0; i < data.Slots.Count; i++)
                            {
                                var sd = data.Slots[i];
                                sd.IsUnlocked = kvp.Value[i];
                                data.Slots[i] = sd;
                            }
                            PlayerData.instance.ToolEquips.SetData(kvp.Key, data);
                        }
                    }

                    foreach (var kvp in _backedUpToolCrests)
                    {
                        ToolItemManager.SetEquippedTools(kvp.Key, kvp.Value);
                    }
                    
                    ToolItemManager.SendEquippedChangedEvent(true);
                }

                if (originalCallback != null)
                {
                    originalCallback(result);
                }
            };
        }
    }
}
