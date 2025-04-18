using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using ScheduleOne.Casino;
using ScheduleOne.Casino.UI;
using ScheduleOne.Money;
using FishNet.Connection;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using MelonLoader;
using ScheduleOne.Map;

namespace CasinoPlusMono
{
    public class casino : MelonMod
    {
        public const float BJ_MIN_BET = 1000f;
        public const float BJ_MAX_BET = 25000f;
        public const float RTB_MIN_BET = 1000f;
        public const float RTB_MAX_BET = 25000f;
        public static readonly int[] SLOT_BET_AMOUNTS = new int[]
        {
            100, 150, 200, 250, 300, 350, 400, 450, 500,
            750, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000
        };

        public static casino Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            MelonLogger.Msg(string.Format("Mod loaded."));
            MelonLogger.Msg(string.Format("Contact aybigRick on Discord about any issues."));

            GameObject enforcerObj = GameObject.Find("ScorePositionEnforcer");
            if (enforcerObj == null)
            {
                enforcerObj = new GameObject("ScorePositionEnforcer");
                enforcerObj.AddComponent<ScorePositionEnforcer>();
                UnityEngine.Object.DontDestroyOnLoad(enforcerObj);
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.Equals("Main", StringComparison.OrdinalIgnoreCase))
            {
                MelonCoroutines.Start(UpdateCasinoSignTextCoroutine());
            }
        }

        private IEnumerator UpdateCasinoSignTextCoroutine()
        {
            yield return new WaitForSeconds(1.0f);
            string sceneName = "Main";
            string path1 = "Map/Container/Casino/casino/DoorWall/OpeningHoursSign/Name";
            string path2 = "Map/Container/Casino/casino/DoorWall (1)/OpeningHoursSign/Name";

            GameObject signObj1 = FindGameObjectInScene(sceneName, path1);
            if (signObj1 != null)
            {
                TextMeshPro tmp1 = signObj1.GetComponent<TextMeshPro>();
                if (tmp1 != null && tmp1.text.Trim().Equals("4PM-5AM", StringComparison.OrdinalIgnoreCase))
                {
                    tmp1.text = "OPEN 24/7";
                    tmp1.ForceMeshUpdate();
                }
            }

            GameObject signObj2 = FindGameObjectInScene(sceneName, path2);
            if (signObj2 != null)
            {
                TextMeshPro tmp2 = signObj2.GetComponent<TextMeshPro>();
                if (tmp2 != null && tmp2.text.Trim().Equals("4PM-5AM", StringComparison.OrdinalIgnoreCase))
                {
                    tmp2.text = "OPEN 24/7";
                    tmp2.ForceMeshUpdate();
                }
            }
        }

        private GameObject RecursiveFind(GameObject current, string[] segments, int index)
        {
            if (current == null || index >= segments.Length)
                return null;
            if (!current.name.Equals(segments[index], StringComparison.OrdinalIgnoreCase))
                return null;
            if (index == segments.Length - 1)
                return current;
            foreach (Transform child in current.transform)
            {
                GameObject result = RecursiveFind(child.gameObject, segments, index + 1);
                if (result != null)
                    return result;
            }
            return null;
        }

        private GameObject FindGameObjectInScene(string sceneName, string path)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            string[] segments = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject result = RecursiveFind(root, segments, 0);
                if (result != null)
                    return result;
            }
            return null;
        }

        public IEnumerator FixPlayerScoreExplicitCoroutine()
        {
            float timer = 0f, timeout = 10f;
            string sceneName = "Main";
            string targetPath = "UI/CasinoGames/Blackjack/Container/ScoresContainer/Player/Score";
            GameObject playerScoreObj = null;
            while (timer < timeout)
            {
                playerScoreObj = FindGameObjectInScene(sceneName, targetPath);
                if (playerScoreObj != null)
                {
                    RectTransform playerRect = playerScoreObj.GetComponent<RectTransform>();
                    if (playerRect != null)
                    {
                        playerRect.localPosition = new Vector3(75f, 0f, 0f);
                        playerRect.anchoredPosition = new Vector2(75f, 0f);
                        yield break;
                    }
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator FixDealerScoreExplicitCoroutine()
        {
            float timer = 0f, timeout = 10f;
            string sceneName = "Main";
            string targetPath = "UI/CasinoGames/Blackjack/Container/ScoresContainer/Dealer/Score";
            GameObject dealerScoreObj = null;
            while (timer < timeout)
            {
                dealerScoreObj = FindGameObjectInScene(sceneName, targetPath);
                if (dealerScoreObj != null)
                {
                    RectTransform dealerRect = dealerScoreObj.GetComponent<RectTransform>();
                    if (dealerRect != null)
                    {
                        dealerRect.localPosition = new Vector3(75f, 0f, 0f);
                        dealerRect.anchoredPosition = new Vector2(75f, 0f);
                        yield break;
                    }
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator FixPayoutTextExplicitCoroutine()
        {
            float timer = 0f, timeout = 10f;
            string sceneName = "Main";
            string targetPath = "UI/CasinoGames/Blackjack/Container/PositiveOutcome/Payout";
            GameObject payoutObj = null;
            while (timer < timeout)
            {
                payoutObj = FindGameObjectInScene(sceneName, targetPath);
                if (payoutObj != null)
                {
                    TMP_Text payoutText = payoutObj.GetComponent<TMP_Text>();
                    if (payoutText != null)
                    {
                        payoutText.textWrappingMode = TextWrappingModes.NoWrap;
                        payoutText.ForceMeshUpdate();
                        yield break;
                    }
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator FixRideTheBusPayoutTextExplicitCoroutine()
        {
            float timer = 0f, timeout = 10f;
            string sceneName = "Main";
            string targetPath = "UI/CasinoGames/RideTheBus/Container/PositiveOutcome/Payout";
            GameObject payoutObj = null;
            while (timer < timeout)
            {
                payoutObj = FindGameObjectInScene(sceneName, targetPath);
                if (payoutObj != null)
                {
                    TMP_Text payoutText = payoutObj.GetComponent<TMP_Text>();
                    if (payoutText != null)
                    {
                        payoutText.textWrappingMode = TextWrappingModes.NoWrap;
                        payoutText.ForceMeshUpdate();
                        yield break;
                    }
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }
    }
}

[HarmonyPatch(typeof(AccessZone), "SetIsOpen")]
public static class ForceCasinoOpenPatch
{
    static void Prefix(AccessZone __instance, ref bool open)
    {
        if (__instance.gameObject.name.Equals("Casino", StringComparison.OrdinalIgnoreCase) ||
            __instance.gameObject.name.Equals("Casino (Closed)", StringComparison.OrdinalIgnoreCase))
        {
            open = true;
        }
    }
}

[HarmonyPatch(typeof(TimedAccessZone), "Start")]
public static class TimedAccessZoneTimePatch
{
    static void Postfix(TimedAccessZone __instance)
    {
        if (__instance.gameObject.name.Equals("Casino", StringComparison.OrdinalIgnoreCase) ||
            __instance.gameObject.name.Equals("Casino (Closed)", StringComparison.OrdinalIgnoreCase))
        {
            __instance.OpenTime = 600;
            __instance.CloseTime = 500;
        }
    }
}

namespace CasinoPlusMono
{
    public class ScorePositionEnforcer : MonoBehaviour
    {
        void LateUpdate() { }
    }
}