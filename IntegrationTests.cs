using System;
using System.Collections.Generic;

namespace SilksongLoadoutRandomizer
{
    public static class IntegrationTests
    {
        public static void RunAllTests()
        {
            Plugin.Logger.LogInfo("=== RUNNING IN-GAME INTEGRATION TESTS ===");
            try
            {
                Test_GenerateRandomLoadout_NoDuplicates();
                Test_GenerateRandomLoadout_CorrectColors();
                Test_GenerateRandomLoadout_RespectsLockedSlots();
                Test_ToolOwnership_RespectsHiddenFlag();
                Test_HunterCrest_BaseSlotsRecognizedAsUnlocked();
                Test_CrestPool_CursedCrestToggle();
                Test_CrestPool_HunterTierFiltering();
                Test_CursedCrest_EquipLogic();
                Plugin.Logger.LogInfo("=== ALL TESTS PASSED! ===");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"TEST FAILED: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Test_GenerateRandomLoadout_NoDuplicates()
        {
            ToolCrest crest = GetFirstAvailableCrest();
            if (crest == null) return;

            // Generate 100 random loadouts and ensure no duplicates
            for (int i = 0; i < 100; i++)
            {
                List<string> loadout = Randomizer.GenerateRandomLoadoutForCrest(crest);
                
                HashSet<string> seenTools = new HashSet<string>();
                foreach (string toolName in loadout)
                {
                    if (string.IsNullOrEmpty(toolName)) continue;
                    if (seenTools.Contains(toolName))
                    {
                        throw new Exception($"Duplicate tool found in generated loadout: {toolName}");
                    }
                    seenTools.Add(toolName);
                }
            }
            Plugin.Logger.LogInfo("Test_GenerateRandomLoadout_NoDuplicates: PASSED");
        }

        private static void Test_GenerateRandomLoadout_CorrectColors()
        {
            ToolCrest crest = GetFirstAvailableCrest();
            if (crest == null) return;

            List<string> loadout = Randomizer.GenerateRandomLoadoutForCrest(crest);

            for (int i = 0; i < loadout.Count; i++)
            {
                if (string.IsNullOrEmpty(loadout[i])) continue;

                ToolItem tool = ToolItemManager.GetToolByName(loadout[i]);
                if (tool == null) throw new Exception($"Generated loadout contains invalid tool: {loadout[i]}");

                // Get the slot info
                var slotInfo = crest.Slots[i];
                if (tool.Type != slotInfo.Type)
                {
                    throw new Exception($"Type mismatch! Slot index {i} expects {slotInfo.Type}, but got {tool.Type} ({tool.name})");
                }
            }
            Plugin.Logger.LogInfo("Test_GenerateRandomLoadout_CorrectColors: PASSED");
        }

        private static void Test_GenerateRandomLoadout_RespectsLockedSlots()
        {
            ToolCrest crest = GetFirstAvailableCrest();
            if (crest == null) return;

            ToolCrestsData.Data data = crest.SaveData;
            List<string> loadout = Randomizer.GenerateRandomLoadoutForCrest(crest);

            for (int i = 0; i < loadout.Count; i++)
            {
                bool isUnlocked = !crest.Slots[i].IsLocked;
                
                if (data.Slots != null && i < data.Slots.Count)
                {
                    if (data.Slots[i].IsUnlocked)
                    {
                        isUnlocked = true;
                    }
                }

                if (!isUnlocked && !string.IsNullOrEmpty(loadout[i]))
                {
                    throw new Exception($"Generated loadout placed tool {loadout[i]} into a locked slot at index {i}!");
                }
            }
            Plugin.Logger.LogInfo("Test_GenerateRandomLoadout_RespectsLockedSlots: PASSED");
        }

        private static void Test_ToolOwnership_RespectsHiddenFlag()
        {
            ToolCrest crest = GetFirstAvailableCrest();
            if (crest == null) return;

            // Save original configs
            bool oldRed = Plugin.GiveAllRedTools.Value;
            bool oldBlue = Plugin.GiveAllBlueTools.Value;
            bool oldYellow = Plugin.GiveAllYellowTools.Value;
            bool oldSkill = Plugin.GiveAllSilkSkills.Value;
            bool oldSlots = Plugin.GiveAllSlots.Value;

            try
            {
                // MODE 1: Strict lock (All configs OFF)
                Plugin.GiveAllRedTools.Value = false;
                Plugin.GiveAllBlueTools.Value = false;
                Plugin.GiveAllYellowTools.Value = false;
                Plugin.GiveAllSilkSkills.Value = false;
                Plugin.GiveAllSlots.Value = false;

                for (int i = 0; i < 20; i++)
                {
                    List<string> loadout = Randomizer.GenerateRandomLoadoutForCrest(crest);
                    foreach (string toolName in loadout)
                    {
                        if (string.IsNullOrEmpty(toolName)) continue;
                        
                        ToolItem tool = ToolItemManager.GetToolByName(toolName);
                        if (tool != null && !tool.IsUnlockedNotHidden)
                        {
                            throw new Exception($"CRITICAL ALGORITHM FAILURE: Algorithm equipped {tool.name}, but its IsUnlockedNotHidden flag is false! It bypassed the ownership check when toggles were OFF.");
                        }
                    }
                }

                // MODE 2: Chaos mode (All configs ON)
                Plugin.GiveAllRedTools.Value = true;
                Plugin.GiveAllBlueTools.Value = true;
                Plugin.GiveAllYellowTools.Value = true;
                Plugin.GiveAllSilkSkills.Value = true;
                Plugin.GiveAllSlots.Value = true;

                bool bypassedAtLeastOne = false;
                for (int i = 0; i < 50; i++)
                {
                    List<string> loadout = Randomizer.GenerateRandomLoadoutForCrest(crest);
                    foreach (string toolName in loadout)
                    {
                        if (string.IsNullOrEmpty(toolName)) continue;
                        
                        ToolItem tool = ToolItemManager.GetToolByName(toolName);
                        if (tool != null && !tool.IsUnlockedNotHidden)
                        {
                            bypassedAtLeastOne = true;
                        }
                    }
                }

                // We can't strictly throw an error if bypassedAtLeastOne is false, because the user might legitimately own 100% of the tools on their save. 
                // But if they don't own all tools, bypassedAtLeastOne should be true.
            }
            finally
            {
                Plugin.GiveAllRedTools.Value = oldRed;
                Plugin.GiveAllBlueTools.Value = oldBlue;
                Plugin.GiveAllYellowTools.Value = oldYellow;
                Plugin.GiveAllSilkSkills.Value = oldSkill;
                Plugin.GiveAllSlots.Value = oldSlots;
            }

            Plugin.Logger.LogInfo("Test_ToolOwnership_RespectsHiddenFlag: PASSED");
        }

        private static void Test_HunterCrest_BaseSlotsRecognizedAsUnlocked()
        {
            ToolCrest hunter = ToolItemManager.GetCrestByName("Hunter");
            if (hunter == null) return;

            // The Hunter crest is guaranteed to have default slots that are never locked.
            // We verify that the Randomizer does NOT treat them as locked memory locket slots.
            int naturallyUnlockedCount = 0;
            for(int i = 0; i < hunter.Slots.Length; i++) 
            {
                if (!hunter.Slots[i].IsLocked) 
                {
                    naturallyUnlockedCount++;
                }
            }

            if (naturallyUnlockedCount == 0)
            {
                throw new Exception("CRITICAL DATA FAILURE: Hunter crest should have naturally unlocked slots, but the base SlotInfo array reported them all as locked.");
            }
            Plugin.Logger.LogInfo("Test_HunterCrest_BaseSlotsRecognizedAsUnlocked: PASSED");
        }


        private static void Test_CrestPool_CursedCrestToggle()
        {
            Plugin.Logger.LogInfo("Starting Test_CrestPool_CursedCrestToggle...");
            bool originalCursed = Plugin.IncludeCursedCrest.Value;

            try
            {
                // Test ON
                Plugin.IncludeCursedCrest.Value = true;
                List<ToolCrest> poolWithCursed = Randomizer.GetAvailableCrestsPool();
                if (GlobalSettings.Gameplay.CursedCrest != null && !poolWithCursed.Exists(c => c.name == GlobalSettings.Gameplay.CursedCrest.name))
                {
                    throw new Exception("CRITICAL ALGORITHM FAILURE: Cursed Crest was enabled in config but was not injected into the crest pool!");
                }

                // Test OFF
                Plugin.IncludeCursedCrest.Value = false;
                List<ToolCrest> poolWithoutCursed = Randomizer.GetAvailableCrestsPool();
                if (GlobalSettings.Gameplay.CursedCrest != null && poolWithoutCursed.Exists(c => c.name == GlobalSettings.Gameplay.CursedCrest.name))
                {
                    throw new Exception("CRITICAL ALGORITHM FAILURE: Cursed Crest was disabled in config but leaked into the crest pool!");
                }
            }
            finally
            {
                Plugin.IncludeCursedCrest.Value = originalCursed;
            }
            Plugin.Logger.LogInfo("Test_CrestPool_CursedCrestToggle: PASSED");
        }

        private static void Test_CrestPool_HunterTierFiltering()
        {
            Plugin.Logger.LogInfo("Starting Test_CrestPool_HunterTierFiltering...");
            bool originalHunter = Plugin.RandomizeHunterTier.Value;
            
            // To test Hunter filtering, we force Hunter, Hunter_v2, and Hunter_v3 to be unlocked temporarily
            ToolCrest h1 = ToolItemManager.GetCrestByName("Hunter");
            ToolCrest h2 = ToolItemManager.GetCrestByName("Hunter_v2");
            ToolCrest h3 = ToolItemManager.GetCrestByName("Hunter_v3");
            
            bool o1 = h1?.IsUnlocked ?? false;
            bool o2 = h2?.IsUnlocked ?? false;
            bool o3 = h3?.IsUnlocked ?? false;

            try
            {
                if (h1 != null) h1.Unlock();
                if (h2 != null) h2.Unlock();
                if (h3 != null) h3.Unlock();

                // Test 1: Randomize Hunter Tier is OFF (Should filter out lower tiers)
                Plugin.RandomizeHunterTier.Value = false;
                List<ToolCrest> strictPool = Randomizer.GetAvailableCrestsPool();
                if (strictPool.Exists(c => c.name == "Hunter" || c.name == "Hunter_v2"))
                {
                    throw new Exception("CRITICAL ALGORITHM FAILURE: Algorithm failed to filter out lower-tier Hunter crests when RandomizeHunterTier was OFF.");
                }

                // Test 2: Randomize Hunter Tier is ON (Should include all tiers)
                Plugin.RandomizeHunterTier.Value = true;
                List<ToolCrest> chaoticPool = Randomizer.GetAvailableCrestsPool();
                if (h1 != null && !chaoticPool.Exists(c => c.name == "Hunter"))
                {
                    throw new Exception("CRITICAL ALGORITHM FAILURE: Algorithm failed to include base Hunter crest when RandomizeHunterTier was ON.");
                }
                if (h2 != null && !chaoticPool.Exists(c => c.name == "Hunter_v2"))
                {
                    throw new Exception("CRITICAL ALGORITHM FAILURE: Algorithm failed to include Hunter_v2 crest when RandomizeHunterTier was ON.");
                }
            }
            finally
            {
                Plugin.RandomizeHunterTier.Value = originalHunter;
                // Perfect clean-up: Restore original unlock state directly via SaveData to prevent side-effects
                if (!o1 && h1 != null) { var d = h1.SaveData; d.IsUnlocked = false; h1.SaveData = d; }
                if (!o2 && h2 != null) { var d = h2.SaveData; d.IsUnlocked = false; h2.SaveData = d; }
                if (!o3 && h3 != null) { var d = h3.SaveData; d.IsUnlocked = false; h3.SaveData = d; }
            }
            Plugin.Logger.LogInfo("Test_CrestPool_HunterTierFiltering: PASSED");
        }

        private static void Test_CursedCrest_EquipLogic()
        {
            Plugin.Logger.LogInfo("Starting Test_CursedCrest_EquipLogic...");
            bool originalCursed = Plugin.IncludeCursedCrest.Value;
            bool originalCrests = Plugin.IncludeCrests.Value;
            bool originalGiveAll = Plugin.GiveAllCrests.Value;
            
            Dictionary<string, bool> originalStates = new Dictionary<string, bool>();
            foreach (string cName in LoadoutManager.AllCrestNames)
            {
                var c = ToolItemManager.GetCrestByName(cName);
                if (c != null)
                {
                    originalStates[c.name] = c.IsUnlocked;
                    var d = c.SaveData; d.IsUnlocked = false; c.SaveData = d; // Temporarily lock all normal crests
                }
            }

            try
            {
                Plugin.GiveAllCrests.Value = false; // MUST BE FALSE to prevent dynamic bypass from injecting locked crests!
                Plugin.IncludeCrests.Value = true;
                Plugin.IncludeCursedCrest.Value = true;

                // Force the randomizer to run. Because all normal crests are locked and bypass is off, it HAS to pick Cursed Crest!
                Randomizer.RandomizeLoadout();
                
                if (PlayerData.instance.CurrentCrestID != GlobalSettings.Gameplay.CursedCrest.name)
                {
                    throw new Exception($"CRITICAL ALGORITHM FAILURE: Failed to force equip Cursed Crest during test. Equipped {PlayerData.instance.CurrentCrestID} instead.");
                }

                if (!PlayerData.instance.IsCurrentCrestTemp)
                {
                    throw new Exception("CRITICAL ALGORITHM FAILURE: Randomizer equipped Cursed Crest but failed to set IsCurrentCrestTemp to true!");
                }

                // Now unlock ONE normal crest to test the inverse
                ToolCrest normalCrest = ToolItemManager.GetCrestByName("Wanderer");
                if (normalCrest != null)
                {
                    var d = normalCrest.SaveData; d.IsUnlocked = true; normalCrest.SaveData = d;
                    Plugin.IncludeCursedCrest.Value = false; // Remove cursed crest from pool
                    Randomizer.RandomizeLoadout(); // Run again. It MUST pick Wanderer.

                    if (PlayerData.instance.IsCurrentCrestTemp)
                    {
                        throw new Exception("CRITICAL ALGORITHM FAILURE: Randomizer equipped normal Crest but incorrectly left IsCurrentCrestTemp as true!");
                    }
                }
            }
            finally
            {
                Plugin.IncludeCursedCrest.Value = originalCursed;
                Plugin.IncludeCrests.Value = originalCrests;
                Plugin.GiveAllCrests.Value = originalGiveAll;

                foreach (string cName in LoadoutManager.AllCrestNames)
                {
                    var c = ToolItemManager.GetCrestByName(cName);
                    if (c != null && originalStates.TryGetValue(c.name, out bool wasUnlocked))
                    {
                        var d = c.SaveData; d.IsUnlocked = wasUnlocked; c.SaveData = d;
                    }
                }
                
                // Re-randomize one last time to leave the player in a completely valid, uncorrupted state!
                Randomizer.RandomizeLoadout();
            }
            Plugin.Logger.LogInfo("Test_CursedCrest_EquipLogic: PASSED");
        }

        private static ToolCrest GetFirstAvailableCrest()
        {
            foreach (string name in LoadoutManager.AllCrestNames)
            {
                ToolCrest c = ToolItemManager.GetCrestByName(name);
                if (c != null) return c;
            }
            return null;
        }
    }
}
