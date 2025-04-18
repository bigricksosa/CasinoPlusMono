using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TMPro;
using ScheduleOne.Casino;
using ScheduleOne.Casino.UI;
using ScheduleOne.Money;
using MelonLoader;
using UnityEngine.UI;
using System.Linq;
using ScheduleOne.DevUtilities;

namespace CasinoPlusMono
{
    [HarmonyPatch(typeof(BlackjackInterface))]
    public static class SplitPatches
    {
        private static GameObject splitButton;
        private static TextMeshProUGUI splitButtonText;

        // Game controller fields
        private static FieldInfo playerHandField;
        private static FieldInfo player2HandField;
        private static MethodInfo refreshBetMethod;
        private static FieldInfo playerBetField;

        // State tracking
        private static bool initialized;
        private static float originalBetBeforeSplit;
        private static bool isSplitActive;
        private static int currentSplitIndex = 1;

        // UI elements
        private static GameObject splitScore;
        private static GameObject splitScore1;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void Initialize(BlackjackInterface __instance)
        {
            if (initialized || __instance == null) return;

            playerHandField = typeof(BlackjackGameController).GetField("player1Hand", BindingFlags.Instance | BindingFlags.NonPublic);
            player2HandField = typeof(BlackjackGameController).GetField("player2Hand", BindingFlags.Instance | BindingFlags.NonPublic);
            refreshBetMethod = typeof(BlackjackInterface).GetMethod("RefreshDisplayedBet", BindingFlags.Instance | BindingFlags.NonPublic);
            playerBetField = typeof(BlackjackGameController).GetField("<LocalPlayerBet>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

            if (playerHandField == null || player2HandField == null || refreshBetMethod == null || playerBetField == null)
                return;

            CreateSplitButton(__instance);
            initialized = true;
        }

        private static void CreateSplitButton(BlackjackInterface instance)
        {
            if (instance?.HitButton?.gameObject == null) return;

            var existing = instance.HitButton.transform.parent.Find("SplitButton");
            if (existing != null)
            {
                splitButton = existing.gameObject;
                return;
            }

            splitButton = GameObject.Instantiate(instance.HitButton.gameObject, instance.HitButton.transform.parent);
            splitButton.name = "SplitButton";
            var rt = splitButton.GetComponent<RectTransform>();
            var hitRt = instance.HitButton.GetComponent<RectTransform>();

            rt.anchoredPosition = new Vector2(
                hitRt.anchoredPosition.x,
                hitRt.anchoredPosition.y + hitRt.sizeDelta.y * 2 + 20f
            );

            splitButton.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.8f, 1f);
            splitButtonText = splitButton.GetComponentInChildren<TextMeshProUGUI>();

            if (splitButtonText != null)
            {
                splitButtonText.text = "SPLIT";
                splitButtonText.fontSize = 24;
                splitButtonText.fontStyle = FontStyles.Bold;
            }

            splitButton.GetComponent<Button>().onClick.RemoveAllListeners();
            splitButton.GetComponent<Button>().onClick.AddListener(() => OnSplit(instance));
            splitButton.SetActive(false);
        }

        [HarmonyPatch("Open")]
        [HarmonyPostfix]
        private static void OpenPostfix(BlackjackInterface __instance)
        {
            ResetSplitState();
            if (__instance.CurrentGame != null)
            {
                originalBetBeforeSplit = __instance.CurrentGame.LocalPlayerBet;
                playerBetField.SetValue(__instance.CurrentGame, 0f);
                refreshBetMethod.Invoke(__instance, null);
            }
        }

        [HarmonyPatch("LocalPlayerReadyForInput")]
        [HarmonyPostfix]
        private static void EnableSplitWhenAppropriate(BlackjackInterface __instance)
        {
            if (__instance?.CurrentGame == null) return;
            splitButton?.SetActive(CanSplitHand(__instance) && !isSplitActive);
        }

        private static bool CanSplitHand(BlackjackInterface instance)
        {
            var game = instance.CurrentGame;
            var mainHand = playerHandField.GetValue(game) as List<PlayingCard>;

            if (mainHand == null || mainHand.Count != 2) return false;

            return mainHand[0].Value == mainHand[1].Value ||
                   (IsTenValue(mainHand[0]) && IsTenValue(mainHand[1]));
        }

        private static bool IsTenValue(PlayingCard card) =>
            card.Value >= PlayingCard.ECardValue.Ten && card.Value <= PlayingCard.ECardValue.King;

        private static void OnSplit(BlackjackInterface instance)
        {
            if (instance?.CurrentGame == null) return;

            var moneyManager = NetworkSingleton<MoneyManager>.Instance;
            if (moneyManager == null) return;

            float currentBet = instance.CurrentGame.LocalPlayerBet;
            if (currentBet <= 0 || moneyManager.cashBalance < currentBet) return;

            // Deduct additional bet and update total
            moneyManager.ChangeCashBalance(-currentBet, true, false);
            playerBetField.SetValue(instance.CurrentGame, currentBet * 2);
            refreshBetMethod.Invoke(instance, null);

            var game = instance.CurrentGame;
            var mainHand = playerHandField.GetValue(game) as List<PlayingCard>;
            var secondHand = player2HandField.GetValue(game) as List<PlayingCard>;

            if (mainHand == null || secondHand == null || mainHand.Count < 2) return;

            // Split cards
            secondHand.Clear();
            secondHand.Add(mainHand[1]);
            mainHand.RemoveAt(1);

            isSplitActive = true;
            currentSplitIndex = 1;
            splitButton.SetActive(false);

            // Update UI and positions
            CreateOrUpdateSplitScoreUI();
            UpdateCardPositions(game);
            instance.InputContainerCanvasGroup.interactable = true;
        }

        [HarmonyPatch(typeof(BlackjackGameController), "RpcLogic___AddCardToPlayerHand_2801973956")]
        [HarmonyPrefix]
        private static void RedirectHit(ref int playerindex, string cardID)
        {
            if (!isSplitActive) return;
            playerindex = currentSplitIndex == 2 ? 1 : 0;
        }

        [HarmonyPatch("StandClicked")]
        [HarmonyPrefix]
        private static bool StandClickedPrefix(BlackjackInterface __instance)
        {
            if (!isSplitActive) return true;

            if (currentSplitIndex == 1)
            {
                currentSplitIndex = 2;
                __instance.InputContainerCanvasGroup.interactable = true;
                UpdateSplitScoreTexts();
                return false;
            }

            ResetSplitState();
            return true;
        }

        private static void UpdateCardPositions(BlackjackGameController game)
        {
            var mainHand = playerHandField.GetValue(game) as List<PlayingCard>;
            var secondHand = player2HandField.GetValue(game) as List<PlayingCard>;
            var mainPositions = GetCardPositions(game, 0);
            var secondPositions = GetCardPositions(game, 1);

            UpdateHandPositions(mainHand, mainPositions);
            UpdateHandPositions(secondHand, secondPositions);
        }

        private static void UpdateHandPositions(List<PlayingCard> hand, Transform[] positions)
        {
            if (hand == null || positions == null) return;

            for (int i = 0; i < hand.Count && i < positions.Length; i++)
                hand[i].GlideTo(positions[i].position, positions[i].rotation, 0.5f, true);
        }

        private static Transform[] GetCardPositions(BlackjackGameController game, int handIndex) =>
            typeof(BlackjackGameController)
                .GetField($"Player{handIndex + 1}CardPositions", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(game) as Transform[];

        private static void CreateOrUpdateSplitScoreUI()
        {
            var scoreContainer = GameObject.Find("UI/CasinoGames/Blackjack/Container/ScoresContainer/Player")?.transform;
            if (scoreContainer == null) return;

            // Hide original score
            var originalScore = scoreContainer.Find("Score");
            if (originalScore != null) originalScore.gameObject.SetActive(false);

            // Create split scores if needed
            if (splitScore == null)
            {
                splitScore = CreateScoreDisplay(originalScore, scoreContainer, new Vector3(55f, 0f, 0f));
                splitScore.name = "SplitScore";
            }

            if (splitScore1 == null)
            {
                splitScore1 = CreateScoreDisplay(originalScore, scoreContainer, new Vector3(100f, 0f, 0f));
                splitScore1.name = "SplitScore1";
            }

            UpdateSplitScoreTexts();
        }

        private static GameObject CreateScoreDisplay(Transform original, Transform parent, Vector3 position)
        {
            var obj = GameObject.Instantiate(original.gameObject, parent);
            obj.GetComponent<RectTransform>().localPosition = position;
            obj.SetActive(true);
            return obj;
        }

        private static void UpdateSplitScoreTexts()
        {
            var instance = Singleton<BlackjackInterface>.Instance;
            if (instance?.CurrentGame == null) return;

            var game = instance.CurrentGame;
            var mainHand = playerHandField.GetValue(game) as List<PlayingCard>;
            var secondHand = player2HandField.GetValue(game) as List<PlayingCard>;

            UpdateScoreText(splitScore, CalculateHandValue(mainHand));
            UpdateScoreText(splitScore1, CalculateHandValue(secondHand));
        }

        private static void UpdateScoreText(GameObject obj, int value)
        {
            if (obj == null) return;
            var text = obj.GetComponent<TextMeshProUGUI>();
            if (text != null) text.text = value.ToString();
        }

        private static int CalculateHandValue(List<PlayingCard> hand)
        {
            if (hand == null) return 0;
            int total = 0, aces = 0;

            foreach (var card in hand)
            {
                int val = (int)card.Value;
                if (val == 1) { aces++; total += 11; }
                else total += Mathf.Min(val, 10);
            }

            while (total > 21 && aces > 0)
            {
                total -= 10;
                aces--;
            }

            return total;
        }

        [HarmonyPatch(typeof(BlackjackGameController), "RemovePlayerFromCurrentRound")]
        [HarmonyPostfix]
        private static void ResetSplitHands(BlackjackGameController __instance)
        {
            ResetSplitState();
            var secondHand = player2HandField.GetValue(__instance) as List<PlayingCard>;
            secondHand?.Clear();
        }

        private static void ResetSplitState()
        {
            isSplitActive = false;
            currentSplitIndex = 1;

            if (splitScore != null) GameObject.Destroy(splitScore);
            if (splitScore1 != null) GameObject.Destroy(splitScore1);
            splitScore = null;
            splitScore1 = null;
        }
    }
}