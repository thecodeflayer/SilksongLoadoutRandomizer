using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongLoadoutRandomizer
{
    [HarmonyPatch(typeof(HealthManager))]
    public static class BossKillPatch
    {
        private static readonly (string sceneName, string goName)[] BossPatterns =
        {
            // === ACT 1 ===
            ("Tut_03", "Mossbone Mother"),
            ("Weave_03", "Mossbone Mother A"),
            ("Weave_03", "Mossbone Mother B"),
            ("Bone_05", "Bone Beast"),
            ("Bone_East_12", "Lace Boss1"),
            ("Bone_East_08", "SG_head"),
            ("Ant_19", "Bone Flyer Giant"),
            ("Greymoor_08", "Vampire Gnat"),
            ("Greymoor_05", "Vampire Gnat"),
            ("Shellwood_18", "Splinter Queen"),
            ("Bone_15", "Skull King"),
            ("Bonetown", "Skull King"),
            ("Coral_11", "Driller A"),
            ("Belltown_Shrine", "Spinner Boss"),
            ("Coral_Judge_Arena", "Last Judge"),
            ("Organ_01", "Phantom"),

            // === ACT 2 ===
            ("Cog_Dancers", "Dancer A"),
            ("Cog_Dancers", "Dancer B"),
            ("Library_13", "Trobbio"),
            ("Bone_East_08", "Bone Flyer Giant"),
            ("Ward_02", "Conductor Boss"),
            ("Dust_Chef", "Roachkeeper Chef (1)"),
            ("Coral_29", "Zap Core Enemy"),
            ("Coral_27", "Coral Conch Driller Giant Solo"),
            ("Slab_16b", "Slab Fly Broodmother"),
            ("Hang_17b", "Song Knight"),
            ("Shadow_18", "Swamp Shaman"),
            ("Slab_10b", "First Weaver"),
            ("Library_09", "Garmond Fighter"),
            ("Greymoor_08", "Mapper Spar NPC"),
            ("Song_Tower_01", "Lace Boss2 New"),
            ("Cradle_03", "Silk Boss"),
            ("Dock_09", "Dock Guard Slasher"),
            ("Dock_09", "Dock Guard Thrower"),
            ("Bone_Steel_Servant", "Abyss Mass"),

            // === ACT 3 ===
            ("wisp02", "WispPyreEffigy"),
            ("Bellway_Centipede_Arena", "Giant Centipede Head"),
            ("Bellway_Centipede_Arena", "Giant Centipede Butt"),
            ("Peak_07", "Pinstress Boss"),
            ("Library_13", "Tormented Trobbio"),
            ("Coral_33", "Garmond Black Threaded Fighter"),
            ("Crawl_10", "Blue Assistant"),
            ("Room_CrowCourt_02", "Crawfather"),
            ("Memory_Coral_Tower", "Coral King"),
            ("Memory_Ant_Queen", "Hunter Queen Boss"),
            ("Bone_East_18b", "Bone Hunter Trapper"),
            ("Shellwood_22", "Seth"),
            ("Shellwood_11b_Memory", "Flower Queen Boss"),
            ("Clover_19", "Cloverstag White Boss"),
            ("Clover_10", "Dancer A"),
            ("Coral_39", "Coral Warrior Grey"),
            ("Abyss_Cocoon", "Lost Lace Boss")
        };

        private static bool IsBoss(GameObject go)
        {
            string normalizedName = go.name.Replace("(Clone)", "");
            foreach (var pattern in BossPatterns)
            {
                if (normalizedName == pattern.goName && SceneManager.GetSceneByName(pattern.sceneName).isLoaded)
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPatch(nameof(HealthManager.Die))]
        [HarmonyPatch(new System.Type[] { typeof(System.Nullable<float>), typeof(AttackTypes), typeof(NailElements), typeof(UnityEngine.GameObject), typeof(bool), typeof(float), typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        public static void Postfix_Die(HealthManager __instance)
        {
            if (!Plugin.IsModEnabled.Value || !Plugin.RandomizeOnBossKill.Value) return;

            if (IsBoss(__instance.gameObject))
            {
                // Check if there are any other living bosses in the scene
                bool otherBossesAlive = false;
                foreach (HealthManager hm in UnityEngine.Object.FindObjectsOfType<HealthManager>())
                {
                    if (hm != __instance && hm.hp > 0 && IsBoss(hm.gameObject))
                    {
                        otherBossesAlive = true;
                        break;
                    }
                }

                if (!otherBossesAlive)
                {
                    Plugin.Logger.LogInfo($"Boss {__instance.gameObject.name} was defeated and no other bosses remain! Triggering randomizer...");
                    Randomizer.RandomizeLoadout();
                }
                else
                {
                    Plugin.Logger.LogInfo($"Boss {__instance.gameObject.name} was defeated, but other boss entities are still alive (duo fight). Waiting...");
                }
            }
        }
    }
}
