using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using ScheduleOne.Casino;
using ScheduleOne.Casino.UI;
using ScheduleOne.Money;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using MelonLoader;
using static CasinoPlusMono.casino;

namespace CasinoPlusMono
{
    [HarmonyPatch(typeof(BlackjackInterface))]
    public static class BlackjackInterfacePatches
    {
        public static GameObject doubleDownButton;
        static TextMeshProUGUI doubleDownText;
        static MethodInfo hitMethod;
        static MethodInfo standMethod;
        static FieldInfo playerHandField;
        static MethodInfo refreshBetMethod;
        static FieldInfo playerBetField;
        static bool initialized = false;
        static float originalBetBeforeDoubleDown;

        static void MoveLabelUp(Transform label, float yOffset)
        {
            if (label != null)
                label.localPosition += new Vector3(0, yOffset, 0);
        }

        [HarmonyPatch("GetBetFromSliderValue")]
        [HarmonyPrefix]
        public static bool GetBetFromSliderValuePrefix(ref float sliderVal, ref float __result)
        {
            __result = Mathf.Lerp(BJ_MIN_BET, BJ_MAX_BET, sliderVal);
            return false;
        }

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Initialize(BlackjackInterface __instance)
        {
            if (initialized || __instance == null)
                return;
            hitMethod = typeof(BlackjackInterface).GetMethod("HitClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            standMethod = typeof(BlackjackInterface).GetMethod("StandClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            playerHandField = typeof(BlackjackGameController).GetField("player1Hand", BindingFlags.NonPublic | BindingFlags.Instance);
            refreshBetMethod = typeof(BlackjackInterface).GetMethod("RefreshDisplayedBet", BindingFlags.NonPublic | BindingFlags.Instance);
            playerBetField = typeof(BlackjackGameController).GetField("<LocalPlayerBet>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hitMethod == null || standMethod == null || playerHandField == null || refreshBetMethod == null || playerBetField == null)
                return;
            CreateDoubleDownButton(__instance);
            initialized = true;
        }

        static void CreateDoubleDownButton(BlackjackInterface __instance)
        {
            if (__instance == null || __instance.HitButton == null || __instance.HitButton.gameObject == null)
                return;
            var existingButton = __instance.HitButton.transform.parent.Find("DoubleDownButton");
            if (existingButton != null)
            {
                doubleDownButton = existingButton.gameObject;
                return;
            }
            doubleDownButton = UnityEngine.Object.Instantiate(__instance.HitButton.gameObject, __instance.HitButton.transform.parent);
            doubleDownButton.name = "DoubleDownButton";
            RectTransform ddTransform = doubleDownButton.GetComponent<RectTransform>();
            RectTransform hitTransform = __instance.HitButton.GetComponent<RectTransform>();
            ddTransform.anchoredPosition = new Vector2(
                hitTransform.anchoredPosition.x,
                hitTransform.anchoredPosition.y + hitTransform.sizeDelta.y + 10f);
            Image ddImage = doubleDownButton.GetComponent<Image>();
            ddImage.color = new Color(0.2f, 0.4f, 0.8f, 1f);
            doubleDownText = doubleDownButton.GetComponentInChildren<TextMeshProUGUI>();
            if (doubleDownText != null)
            {
                doubleDownText.text = "DOUBLE DOWN";
                doubleDownText.fontSize = 24;
                doubleDownText.fontStyle = FontStyles.Bold;
            }
            Button ddButton = doubleDownButton.GetComponent<Button>();
            ddButton.onClick.RemoveAllListeners();
            ddButton.onClick.AddListener(() => OnDoubleDown(__instance));
            var inputContainer = __instance.InputContainerCanvasGroup.GetComponent<RectTransform>();
            if (inputContainer != null)
                inputContainer.sizeDelta += new Vector2(0, 60);
            MoveLabelUp(__instance.PlayerScoreLabel?.transform, 30);
            MoveLabelUp(__instance.DealerScoreLabel?.transform, 30);
        }

        [HarmonyPatch("RefreshDisplayedBet")]
        [HarmonyPrefix]
        public static bool RefreshDisplayedBetPrefix(BlackjackInterface __instance)
        {
            try
            {
                if (__instance == null || __instance.CurrentGame == null || __instance.BetAmount == null || __instance.BetSlider == null)
                    return false;
                float currentBet = __instance.CurrentGame.LocalPlayerBet;
                string formattedBet = MoneyManager.FormatAmount(currentBet, false, false);
                __instance.BetAmount.text = formattedBet;
                __instance.BetAmount.ForceMeshUpdate();
            }
            catch (Exception) { }
            return false;
        }

        [HarmonyPatch("Open")]
        [HarmonyPostfix]
        public static void OpenPostfix(BlackjackInterface __instance)
        {
            if (__instance == null || __instance.CurrentGame == null)
                return;
            originalBetBeforeDoubleDown = __instance.CurrentGame.LocalPlayerBet;
            __instance.CurrentGame.SetLocalPlayerBet(BJ_MIN_BET);
            __instance.BetSlider.minValue = 0;
            __instance.BetSlider.maxValue = 1;
            __instance.BetSlider.SetValueWithoutNotify(0);
            refreshBetMethod?.Invoke(__instance, null);
            if (__instance.HitButton != null)
                __instance.HitButton.gameObject.SetActive(true);
            if (__instance.StandButton != null)
                __instance.StandButton.gameObject.SetActive(true);
            if (doubleDownButton != null)
                doubleDownButton.SetActive(true);
            else
                CreateDoubleDownButton(__instance);
            UpdateTopBetUI(__instance);
            MelonCoroutines.Start(casino.Instance.FixPlayerScoreExplicitCoroutine());
            MelonCoroutines.Start(casino.Instance.FixDealerScoreExplicitCoroutine());
            MelonCoroutines.Start(casino.Instance.FixPayoutTextExplicitCoroutine());
        }

        [HarmonyPatch("LocalPlayerReadyForInput")]
        [HarmonyPostfix]
        public static void EnableDoubleDownWhenAppropriate(BlackjackInterface __instance)
        {
            if (__instance == null || __instance.CurrentGame == null)
                return;
            if (doubleDownButton == null)
            {
                CreateDoubleDownButton(__instance);
                if (doubleDownButton == null)
                    return;
            }
            doubleDownButton.SetActive(true);
        }

        [HarmonyPatch("LocalPlayerExitRound")]
        [HarmonyPostfix]
        public static void HideDoubleDownBetweenRounds(BlackjackInterface __instance)
        {
            if (doubleDownButton != null)
                doubleDownButton.SetActive(false);
            if (__instance != null && __instance.CurrentGame != null && playerBetField != null)
            {
                playerBetField.SetValue(__instance.CurrentGame, originalBetBeforeDoubleDown);
                refreshBetMethod?.Invoke(__instance, null);
            }
            if (__instance.HitButton != null)
                __instance.HitButton.gameObject.SetActive(true);
            if (__instance.StandButton != null)
                __instance.StandButton.gameObject.SetActive(true);
            if (__instance.BetSlider != null)
                __instance.BetSlider.SetValueWithoutNotify(0);
            UpdateTopBetUI(__instance);
            MelonCoroutines.Start(casino.Instance.FixPlayerScoreExplicitCoroutine());
            MelonCoroutines.Start(casino.Instance.FixDealerScoreExplicitCoroutine());
            MelonCoroutines.Start(casino.Instance.FixPayoutTextExplicitCoroutine());
        }

        static void OnDoubleDown(BlackjackInterface __instance)
        {
            if (__instance == null || __instance.CurrentGame == null)
                return;
            float currentBet = __instance.CurrentGame.LocalPlayerBet;
            if (currentBet <= 0)
                return;
            if (doubleDownButton != null)
                doubleDownButton.SetActive(false);
            var moneyManager = NetworkSingleton<MoneyManager>.Instance;
            if (moneyManager == null)
                return;
            moneyManager.ChangeCashBalance(-currentBet, true, false);
            float newBet = currentBet * 2;
            if (playerBetField == null)
            {
                playerBetField = typeof(BlackjackGameController).GetField("<LocalPlayerBet>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerBetField == null)
                    return;
            }
            playerBetField.SetValue(__instance.CurrentGame, newBet);
            refreshBetMethod?.Invoke(__instance, null);
            UpdateTopBetUI(__instance);
            if (__instance.InputContainerCanvasGroup != null)
                __instance.InputContainerCanvasGroup.interactable = false;
            if (__instance.HitButton != null)
                __instance.HitButton.gameObject.SetActive(false);
            if (__instance.StandButton != null)
                __instance.StandButton.gameObject.SetActive(false);
            __instance.StartCoroutine(RunDoubleDownActions(__instance));
        }

        static void UpdateTopBetUI(BlackjackInterface instance)
        {
            if (instance != null)
            {
                Transform scoreTransform = instance.transform.Find("Container/GameStatus/Container/PlayerDisplay/Players/Player/Container/Score");
                if (scoreTransform != null)
                {
                    TextMeshProUGUI scoreText = scoreTransform.GetComponent<TextMeshProUGUI>();
                    if (scoreText != null && instance.CurrentGame != null)
                    {
                        float bet = instance.CurrentGame.LocalPlayerBet;
                        string formatted = MoneyManager.FormatAmount(bet, false, false);
                        scoreText.text = formatted;
                        scoreText.ForceMeshUpdate();
                    }
                }
            }
        }

        static IEnumerator RunDoubleDownActions(BlackjackInterface __instance)
        {
            yield return new WaitForSeconds(0.2f);
            hitMethod.Invoke(__instance, null);
            yield return new WaitForSeconds(1.0f);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            if (standMethod != null)
                standMethod.Invoke(__instance, null);
            else
                __instance.CurrentGame.LocalPlayerData.SetData<float>("Action", 2f, true);
            yield return new WaitForEndOfFrame();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            yield break;
        }
    }

    [HarmonyPatch(typeof(BlackjackGameController))]
    public static class BlackjackGameControllerPatches
    {
        [HarmonyPatch("SetLocalPlayerBet")]
        [HarmonyPrefix]
        public static void SetLocalPlayerBetPrefix(ref float bet)
        {
            bet = Mathf.Clamp(bet, BJ_MIN_BET, BJ_MAX_BET);
        }
    }
}