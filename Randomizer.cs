using BepInEx.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongLoadoutRandomizer
{
    public static class Randomizer
    {
        // This will be called whenever the player hits a randomizer trigger 
        // (like resting at a bench, or defeating a boss)
        public static void RandomizeLoadout()
        {
            if (!Plugin.IsModEnabled.Value) return;

            Plugin.Logger.LogInfo("Triggering Loadout Randomization...");

            ToolCrest currentCrest = null;

            // 1. Determine which Crest to equip
            if (Plugin.IncludeCrests.Value)
            {
                currentCrest = RandomizeCrest();
            }
            
            if (currentCrest == null)
            {
                currentCrest = ToolItemManager.GetCrestByName(PlayerData.instance.CurrentCrestID);
            }

            // 2. Randomize the Tools (Red, Blue, Yellow) and Silk Skills
            if (currentCrest != null)
            {
                RandomizeToolsAndSkills(currentCrest);
            }
            
            // 3. Force the game engine to recalculate the silk spool limit in case the Cursed Crest was equipped or unequipped
            if (HeroController.instance != null)
            {
                HeroController.instance.UpdateSilkCursed();
            }

            Plugin.Logger.LogInfo("Loadout successfully randomized!");

            ShowNotification("Loadout Randomized");
        }

        public static void ShowNotification(string message)
        {
            // Attempt to display native UI notification
            try
            {
                Sprite diceSprite = null;
                // Try to find the Magnetite Dice to use its icon
                ToolItem diceTool = ToolItemManager.GetToolByName("Magnetite Dice");
                if (diceTool != null)
                {
                    diceSprite = diceTool.GetPopupIcon();
                }

                RandomizerNotificationItem notification = new RandomizerNotificationItem(message, diceSprite);
                CollectableUIMsg.Spawn(notification, null, false);
            }
            catch (System.Exception e)
            {
                Plugin.Logger.LogWarning($"Failed to spawn UI notification: {e.Message}");
            }
        }

        public static void ScrubIllegitimateItems()
        {
            if (PlayerData.instance == null) return;
            Plugin.Logger.LogInfo("Scrubbing illegitimate items from active loadout...");

            ToolCrest currentCrest = ToolItemManager.GetCrestByName(PlayerData.instance.CurrentCrestID);
            
            // Revert cursed crest or unowned crest
            if (currentCrest == GlobalSettings.Gameplay.CursedCrest || currentCrest == null || !currentCrest.IsUnlocked)
            {
                string fallbackCrest = "Hunter";
                foreach (string cName in LoadoutManager.AllCrestNames)
                {
                    ToolCrest c = ToolItemManager.GetCrestByName(cName);
                    if (c != null && c.IsUnlocked)
                    {
                        if (cName == "Hunter_v3") { fallbackCrest = cName; break; }
                        else if (cName == "Hunter_v2" && fallbackCrest == "Hunter") { fallbackCrest = cName; }
                        else if (!cName.StartsWith("Hunter")) { fallbackCrest = cName; break; }
                    }
                }
                ToolItemManager.SetEquippedCrest(fallbackCrest);
            }
            PlayerData.instance.IsCurrentCrestTemp = false;

            // Revert all unowned tools
            var validCrestNames = PlayerData.instance.ToolEquips.GetValidNames();
            foreach (string crestName in LoadoutManager.AllCrestNames)
            {
                if (validCrestNames == null || !validCrestNames.Contains(crestName)) continue;

                List<ToolItem> equips = ToolItemManager.GetEquippedToolsForCrest(crestName);
                if (equips != null)
                {
                    List<string> scrubbedEquips = new List<string>();
                    bool changed = false;
                    for (int i = 0; i < equips.Count; i++)
                    {
                        ToolItem tool = equips[i];
                        if (tool != null && tool.IsUnlockedNotHidden)
                        {
                            scrubbedEquips.Add(tool.name);
                        }
                        else
                        {
                            scrubbedEquips.Add("");
                            if (tool != null) changed = true;
                        }
                    }
                    if (changed)
                    {
                        ToolItemManager.SetEquippedTools(crestName, scrubbedEquips);
                    }
                }
            }

            ToolItemManager.SendEquippedChangedEvent(true);
        }

        public static List<ToolCrest> GetAvailableCrestsPool()
        {
            List<ToolCrest> availableCrests = new List<ToolCrest>();

            foreach (string crestName in LoadoutManager.AllCrestNames)
            {
                ToolCrest c = ToolItemManager.GetCrestByName(crestName);
                if (c != null && (c.IsUnlocked || Plugin.GiveAllCrests.Value))
                {
                    // Ensure the crest actually exists in PlayerData memory to avoid NREs
                    if (PlayerData.instance != null)
                    {
                        var names = PlayerData.instance.ToolEquips.GetValidNames();
                        if (names == null || !names.Contains(crestName))
                        {
                            continue;
                        }
                    }

                    availableCrests.Add(c);
                }
            }

            // Filter out lower tier Hunter crests if higher tiers are unlocked, UNLESS Randomize Hunter Tier is enabled
            if (!Plugin.RandomizeHunterTier.Value)
            {
                bool hasV3 = availableCrests.Exists(c => c.name == "Hunter_v3");
                bool hasV2 = availableCrests.Exists(c => c.name == "Hunter_v2");
                
                if (hasV3)
                {
                    availableCrests.RemoveAll(c => c.name == "Hunter" || c.name == "Hunter_v2");
                }
                else if (hasV2)
                {
                    availableCrests.RemoveAll(c => c.name == "Hunter");
                }
            }

            // Add the cursed crest if the option is enabled. Cursed Crest is a special global instance.
            if (Plugin.IncludeCursedCrest.Value && GlobalSettings.Gameplay.CursedCrest != null)
            {
                availableCrests.Add(GlobalSettings.Gameplay.CursedCrest);
            }

            return availableCrests;
        }

        private static ToolCrest RandomizeCrest()
        {
            List<ToolCrest> availableCrests = GetAvailableCrestsPool();

            if (availableCrests.Count > 0)
            {
                ToolCrest chosenCrest = availableCrests[UnityEngine.Random.Range(0, availableCrests.Count)];
                
                // Equip the crest
                if (chosenCrest.name != PlayerData.instance.CurrentCrestID)
                {
                    PlayerData.instance.PreviousCrestID = PlayerData.instance.CurrentCrestID;
                }
                
                ToolItemManager.SetEquippedCrest(chosenCrest.name);
                
                bool isCursed = chosenCrest == GlobalSettings.Gameplay.CursedCrest;
                PlayerData.instance.IsCurrentCrestTemp = isCursed;

                return chosenCrest;
            }

            return null;
        }

        private static void RandomizeToolsAndSkills(ToolCrest crest)
        {
            List<string> newEquips = GenerateRandomLoadoutForCrest(crest);

            // Apply to the game
            ToolItemManager.SetEquippedTools(crest.name, newEquips);
            ToolItemManager.SendEquippedChangedEvent();

            // Log the exact loadout that was applied
            Plugin.Logger.LogInfo($"[Randomizer] Successfully equipped Crest: {crest.name}");
            Plugin.Logger.LogInfo($"[Randomizer] Equipped Tools:");
            for (int i = 0; i < newEquips.Count; i++)
            {
                string toolStr = string.IsNullOrEmpty(newEquips[i]) ? "[EMPTY]" : newEquips[i];
                Plugin.Logger.LogInfo($"  - Slot {i + 1}: {toolStr}");
            }
        }

        // Extracted for Integration Testing
        public static List<string> GenerateRandomLoadoutForCrest(ToolCrest crest)
        {
            // Build the pools of available tools based on configuration toggles
            List<ToolItem> redPool = new List<ToolItem>();
            List<ToolItem> bluePool = new List<ToolItem>();
            List<ToolItem> yellowPool = new List<ToolItem>();
            List<ToolItem> skillPool = new List<ToolItem>();

            foreach (ToolItem tool in ToolItemManager.GetAllTools())
            {
                bool isUnlocked = tool.IsUnlockedNotHidden;

                if (!isUnlocked)
                {
                    if (tool.Type == ToolItemType.Red && Plugin.GiveAllRedTools.Value) isUnlocked = true;
                    else if (tool.Type == ToolItemType.Blue && Plugin.GiveAllBlueTools.Value) isUnlocked = true;
                    else if (tool.Type == ToolItemType.Yellow && Plugin.GiveAllYellowTools.Value) isUnlocked = true;
                    else if (tool.Type == ToolItemType.Skill && Plugin.GiveAllSilkSkills.Value) isUnlocked = true;
                }

                if (!isUnlocked) continue;

                // The Needle Phial is a temporary quest item and shouldn't take up a slot
                if (tool.name.Replace(" ", "").Replace("_", "").Equals("NeedlePhial", System.StringComparison.OrdinalIgnoreCase)) continue;

                if (tool.Type == ToolItemType.Red && Plugin.IncludeRedTools.Value) redPool.Add(tool);
                else if (tool.Type == ToolItemType.Blue && Plugin.IncludeBlueTools.Value) bluePool.Add(tool);
                else if (tool.Type == ToolItemType.Yellow && Plugin.IncludeYellowTools.Value) yellowPool.Add(tool);
                else if (tool.Type == ToolItemType.Skill && Plugin.IncludeSilkSkills.Value) skillPool.Add(tool);
            }

            List<string> newEquips = new List<string>();
            ToolCrestsData.Data crestData = crest.SaveData;

            // Iterate over the slots of the crest
            for (int i = 0; i < crest.Slots.Length; i++)
            {
                bool isSlotUnlocked = Plugin.GiveAllSlots.Value || !crest.Slots[i].IsLocked; // Start with config or base state

                // Check if save data overrides it (e.g. Memory Lockets)
                if (!isSlotUnlocked && crestData.Slots != null && i < crestData.Slots.Count)
                {
                    if (crestData.Slots[i].IsUnlocked)
                    {
                        isSlotUnlocked = true;
                    }
                }

                if (!isSlotUnlocked)
                {
                    newEquips.Add(""); // Slot is locked, empty it
                    continue;
                }

                // Try to fill the slot from the matching pool
                var slotInfo = crest.Slots[i];
                ToolItem selectedTool = null;

                if (slotInfo.Type == ToolItemType.Red && redPool.Count > 0)
                {
                    int r = UnityEngine.Random.Range(0, redPool.Count);
                    selectedTool = redPool[r];
                    redPool.RemoveAt(r);
                }
                else if (slotInfo.Type == ToolItemType.Blue && bluePool.Count > 0)
                {
                    int r = UnityEngine.Random.Range(0, bluePool.Count);
                    selectedTool = bluePool[r];
                    bluePool.RemoveAt(r);
                }
                else if (slotInfo.Type == ToolItemType.Yellow && yellowPool.Count > 0)
                {
                    int r = UnityEngine.Random.Range(0, yellowPool.Count);
                    selectedTool = yellowPool[r];
                    yellowPool.RemoveAt(r);
                }
                else if (slotInfo.Type == ToolItemType.Skill && skillPool.Count > 0)
                {
                    int r = UnityEngine.Random.Range(0, skillPool.Count);
                    selectedTool = skillPool[r];
                    skillPool.RemoveAt(r);
                }

                if (selectedTool != null)
                {
                    newEquips.Add(selectedTool.name);
                }
                else
                {
                    newEquips.Add(""); // Ran out of tools for this color, leave empty
                }
            }

            return newEquips;
        }
    }
}
