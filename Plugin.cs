using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using HarmonyLib;

namespace SilksongLoadoutRandomizer
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        // 1. General
        public static ConfigEntry<bool> IsModEnabled;
        public static ConfigEntry<bool> RandomizeOnBenchRest;
        public static ConfigEntry<bool> RandomizeOnBossKill;
        public static BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut> RandomizeKeybind;
        
        // 2. Crests
        public static ConfigEntry<bool> IncludeCrests;
        public static ConfigEntry<bool> RandomizeHunterTier;
        public static ConfigEntry<bool> IncludeCursedCrest;
        
        // 3. Silk Skills
        public static ConfigEntry<bool> IncludeSilkSkills;
        
        // 4. Tools
        public static ConfigEntry<bool> IncludeRedTools;
        public static ConfigEntry<bool> IncludeBlueTools;
        public static ConfigEntry<bool> IncludeYellowTools;

        // 5. Unlocks
        public static ConfigEntry<bool> GiveAllCrests;
        public static ConfigEntry<bool> GiveAllSlots;
        public static ConfigEntry<bool> GiveAllSilkSkills;
        public static ConfigEntry<bool> GiveAllRedTools;
        public static ConfigEntry<bool> GiveAllBlueTools;
        public static ConfigEntry<bool> GiveAllYellowTools;

        private void Awake()
        {
            Logger = base.Logger;

            Harmony.CreateAndPatchAll(typeof(SaveGamePatch), MyPluginInfo.PLUGIN_GUID);
            Harmony.CreateAndPatchAll(typeof(BossKillPatch), MyPluginInfo.PLUGIN_GUID);

            // 1. General
            IsModEnabled = Config.Bind("1. General", "Enable Randomizer", true, 
                new BepInEx.Configuration.ConfigDescription("Turn the loadout randomizer on or off.", null, new ConfigurationManagerAttributes { Order = 100 }));
            
            RandomizeOnBenchRest = Config.Bind("1. General", "Randomize On Bench Rest", true, 
                new BepInEx.Configuration.ConfigDescription("If enabled, your loadout is randomized every time you rest at a bench.", null, new ConfigurationManagerAttributes { Order = 90 }));

            RandomizeOnBossKill = Config.Bind("1. General", "Randomize On Boss Kill", false, 
                new BepInEx.Configuration.ConfigDescription("If enabled, your loadout is randomized every time you defeat a boss.", null, new ConfigurationManagerAttributes { Order = 85 }));

            RandomizeKeybind = Config.Bind("1. General", "Randomize Keybind", new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.F10), 
                new BepInEx.Configuration.ConfigDescription("Press this key to instantly randomize your loadout at any time.", null, new ConfigurationManagerAttributes { Order = 80 }));
            
            // 2. Crests
            IncludeCrests = Config.Bind("2. Crests", "Randomize Crests", true, 
                new BepInEx.Configuration.ConfigDescription("Should crests be randomized?", null, new ConfigurationManagerAttributes { Order = 80 }));

            RandomizeHunterTier = Config.Bind("2. Crests", "Randomize Hunter Tier", false, 
                new BepInEx.Configuration.ConfigDescription("If true, allows lower tiers of the Hunter Crest to be chosen even if you have unlocked a higher tier.", null, new ConfigurationManagerAttributes { Order = 75 }));

            IncludeCursedCrest = Config.Bind("2. Crests", "Include Cursed Crest", false, 
                new BepInEx.Configuration.ConfigDescription("If true, the Cursed Crest can be chosen when crests are randomized.", null, new ConfigurationManagerAttributes { Order = 70 }));

            // 3. Silk Skills
            IncludeSilkSkills = Config.Bind("3. Silk Skills", "Randomize Silk Skills", true, 
                new BepInEx.Configuration.ConfigDescription("Should Silk Skills be randomized?", null, new ConfigurationManagerAttributes { Order = 60 }));
                
            // 4. Tools
            IncludeRedTools = Config.Bind("4. Tools", "Randomize Red Tools", true, 
                new BepInEx.Configuration.ConfigDescription("Should Red Tools be randomized?", null, new ConfigurationManagerAttributes { Order = 50 }));
            
            IncludeBlueTools = Config.Bind("4. Tools", "Randomize Blue Tools", true, 
                new BepInEx.Configuration.ConfigDescription("Should Blue Tools be randomized?", null, new ConfigurationManagerAttributes { Order = 40 }));
            
            IncludeYellowTools = Config.Bind("4. Tools", "Randomize Yellow Tools", true, 
                new BepInEx.Configuration.ConfigDescription("Should Yellow Tools be randomized?", null, new ConfigurationManagerAttributes { Order = 30 }));

            // 5. Unlocks
            GiveAllCrests = Config.Bind("5. Unlocks", "Unlock All Crests", false, 
                new BepInEx.Configuration.ConfigDescription("Automatically gives the player all Crests.", null, new ConfigurationManagerAttributes { Order = 20 }));
            
            GiveAllSlots = Config.Bind("5. Unlocks", "Unlock All Slots", false, 
                new BepInEx.Configuration.ConfigDescription("Automatically unlocks all memory locket slots on all crests.", null, new ConfigurationManagerAttributes { Order = 19 }));
            
            GiveAllSilkSkills = Config.Bind("5. Unlocks", "Unlock All Silk Skills", false, 
                new BepInEx.Configuration.ConfigDescription("Automatically gives the player all Silk Skills.", null, new ConfigurationManagerAttributes { Order = 18 }));
            
            GiveAllRedTools = Config.Bind("5. Unlocks", "Unlock All Red Tools", false, 
                new BepInEx.Configuration.ConfigDescription("Automatically gives the player all Red Tools.", null, new ConfigurationManagerAttributes { Order = 17 }));
            
            GiveAllBlueTools = Config.Bind("5. Unlocks", "Unlock All Blue Tools", false, 
                new BepInEx.Configuration.ConfigDescription("Automatically gives the player all Blue Tools.", null, new ConfigurationManagerAttributes { Order = 16 }));
            
            GiveAllYellowTools = Config.Bind("5. Unlocks", "Unlock All Yellow Tools", false, 
                new BepInEx.Configuration.ConfigDescription("Automatically gives the player all Yellow Tools.", null, new ConfigurationManagerAttributes { Order = 15 }));

            Config.SettingChanged += (sender, args) => {
                Randomizer.ScrubIllegitimateItems();
                if (HeroController.instance != null) {
                    HeroController.instance.UpdateSilkCursed();
                }
            };



            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded and configured!");
        }

        private bool _wasAtBench = false;
        private bool _notifiedStartup = false;

        private void Update()
        {
            // Ensure the game is actually running, Hornet exists, and a save file is fully loaded
            if (HeroController.instance != null && PlayerData.instance != null && GameManager.instance != null && GameManager.instance.IsGameplayScene())
            {
                if (!_notifiedStartup)
                {
                    _notifiedStartup = true;
                    _wasAtBench = PlayerData.instance.atBench; // Prevent randomization on load

                    if (IsModEnabled.Value)
                    {
                        Randomizer.ShowNotification("Loadout Randomizer Enabled");
                    }
                }

                bool isAtBench = PlayerData.instance.atBench;
                
                // Detect the exact frame Hornet transitions from standing to sitting at a bench
                if (isAtBench && !_wasAtBench)
                {
                    if (IsModEnabled.Value && RandomizeOnBenchRest.Value)
                    {
                        Logger.LogInfo("Hornet sat at a bench! Triggering randomizer...");
                        Randomizer.RandomizeLoadout();
                    }
                }

                _wasAtBench = isAtBench;

                if (IsModEnabled.Value && RandomizeKeybind.Value.IsDown())
                {
                    Logger.LogInfo("Randomizer keybind pressed! Triggering randomizer...");
                    Randomizer.RandomizeLoadout();
                }
            }
            else if (HeroController.instance == null || GameManager.instance == null || !GameManager.instance.IsGameplayScene())
            {
                _notifiedStartup = false;
            }
        }
    }

    /// <summary>
    /// Class that specifies how a setting should be displayed inside the ConfigurationManager UI.
    /// It must be a public class so the ConfigurationManager can read it via reflection.
    /// </summary>
    public sealed class ConfigurationManagerAttributes
    {
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
        public object Browsable;
        public bool? HideDefaultButton;
        public int? Order;
    }
}
