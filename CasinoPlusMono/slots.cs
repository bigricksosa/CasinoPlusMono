using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using TMPro;
using MelonLoader;
using static CasinoPlusMono.casino;
using ScheduleOne.Casino;

namespace CasinoPlusMono
{
    [HarmonyPatch(typeof(SlotMachine))]
    public static class SlotMachinePatches
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void ModifyBetAmounts(SlotMachine __instance)
        {
            typeof(SlotMachine)
                .GetField("BetAmounts", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.SetValue(null, SLOT_BET_AMOUNTS);
            __instance.SetBetIndex(null, 0);
            FixTextDisplay(__instance.BetAmountLabel);
        }

        [HarmonyPatch("SetBetIndex")]
        [HarmonyPostfix]
        public static void AfterSetBetIndex(SlotMachine __instance)
        {
            FixTextDisplay(__instance.BetAmountLabel);
        }

        static void FixTextDisplay(TextMeshPro textComponent)
        {
            if (textComponent != null)
            {
                var originalFontSize = textComponent.fontSize;
                var originalPosition = textComponent.transform.localPosition;
                var originalRectSize = textComponent.rectTransform.sizeDelta;
                textComponent.enableWordWrapping = false;
                textComponent.overflowMode = TextOverflowModes.Overflow;
                textComponent.alignment = TextAlignmentOptions.Center;
                textComponent.margin = new Vector4(0, 0, 0, 0);
                textComponent.enableAutoSizing = false;
                textComponent.fontSize = originalFontSize;
                textComponent.transform.localPosition = originalPosition;
                textComponent.rectTransform.sizeDelta = originalRectSize;
                textComponent.ForceMeshUpdate();
                textComponent.rectTransform.ForceUpdateRectTransforms();
            }
        }
    }
}