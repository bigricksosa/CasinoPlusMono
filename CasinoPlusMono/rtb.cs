using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using ScheduleOne.Casino;
using ScheduleOne.Casino.UI;
using ScheduleOne.Money;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using UnityEngine.EventSystems;
using static CasinoPlusMono.casino;

namespace CasinoPlusMono
{
    [HarmonyPatch(typeof(RTBInterface))]
    public static class RTBInterfacePatches
    {
        [HarmonyPatch("GetBetFromSliderValue")]
        [HarmonyPrefix]
        public static bool GetBetFromSliderValuePrefix(ref float sliderVal, ref float __result)
        {
            __result = Mathf.Lerp(RTB_MIN_BET, RTB_MAX_BET, sliderVal);
            return false;
        }

        [HarmonyPatch("RefreshDisplayedBet")]
        [HarmonyPrefix]
        public static bool RefreshDisplayedBetPrefix(RTBInterface __instance)
        {
            try
            {
                float currentBet = __instance.CurrentGame.LocalPlayerBet;
                __instance.BetAmount.text = MoneyManager.FormatAmount(currentBet, false, false);
                __instance.BetAmount.ForceMeshUpdate();
                float sliderPos = Mathf.InverseLerp(RTB_MIN_BET, RTB_MAX_BET, currentBet);
                __instance.BetSlider.SetValueWithoutNotify(sliderPos);
            }
            catch (Exception) { }
            return false;
        }

        [HarmonyPatch("Open")]
        [HarmonyPostfix]
        public static void OpenPostfix(RTBInterface __instance)
        {
            __instance.CurrentGame.SetLocalPlayerBet(RTB_MIN_BET);
            typeof(RTBInterface)
                .GetMethod("RefreshDisplayedBet", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(__instance, null);
            MelonCoroutines.Start(casino.Instance.FixRideTheBusPayoutTextExplicitCoroutine());
        }
    }

    [HarmonyPatch(typeof(RTBGameController))]
    public static class RTBGameControllerPatches
    {
        [HarmonyPatch("SetLocalPlayerBet")]
        [HarmonyPrefix]
        public static void SetLocalPlayerBetPrefix(ref float bet)
        {
            bet = Mathf.Clamp(bet, RTB_MIN_BET, RTB_MAX_BET);
        }
    }
}