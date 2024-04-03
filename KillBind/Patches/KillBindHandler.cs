﻿using DunGen;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static KillBind.Initialise;

namespace KillBind.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    public class KillBindHandler
    {
        private static PlayerControllerB PlayerControllerBInstance;
        private static Terminal TerminalInstance;
        public static StartOfRound StartOfRoundInstance;

        private static Vector3 PositionLastFrame;
        private static Vector3 PositionCurrentFrame;
        private static Vector3 RagdollVelocity;
        private static readonly float VelocityMultiplier = 46;

        [HarmonyPatch("ConnectClientToPlayerObject")]
        public static void Postfix(PlayerControllerB __instance)
        {
            if (__instance == GameNetworkManager.Instance.localPlayerController)
            {
                PlayerControllerBInstance = __instance;
                TerminalInstance = UnityEngine.Object.FindObjectOfType<Terminal>();
                StartOfRoundInstance = StartOfRound.Instance;

                InputActionInstance.ActionKillBind.performed += OnKeyPress;
                modLogger.LogInfo("KillBind has been bound");
            }
        }

        [HarmonyPatch(nameof(PlayerControllerB.OnDestroy))]
        private static void Postfix()
        {
            InputActionInstance.ActionKillBind.performed -= OnKeyPress; //i think (i'm not sure) it would create a memory leak otherwise
        }

        public static void OnKeyPress(InputAction.CallbackContext callbackContext)
        {
            //Check if situation is valid
            if (!callbackContext.performed) { return; }
            if (PlayerControllerBInstance != GameNetworkManager.Instance.localPlayerController) { return; };
            if (PlayerControllerBInstance.isPlayerDead) { return; };
            if (HUDManager.Instance.typingIndicator.enabled || PlayerControllerBInstance.isTypingChat) { return; };
            if (TerminalInstance.terminalInUse && PlayerControllerBInstance.inTerminalMenu) { return; };

            //Check if current config is valid
            if (ModSettings.DeathCause.Value > Enum.GetValues(typeof(CauseOfDeath)).Length || ModSettings.DeathCause.Value < 0) //If your choice is invalid, set to default (unknown death cause)
            {
                ModSettings.DeathCause.Value = (int)ModSettings.DeathCause.DefaultValue;
                modLogger.LogInfo("Your config for HeadType is invalid, reverting to default");
            }

            if (ModSettings.RagdollType.Value > StartOfRoundInstance.playerRagdolls.Count || ModSettings.RagdollType.Value < 0) //If your choice is invalid, set to default (explode head)
            {
                ModSettings.RagdollType.Value = (int)ModSettings.RagdollType.DefaultValue;
                modLogger.LogInfo("Your config for HeadType is invalid, reverting to default");
            }

            //Run KillPlayer
            CoroutineHelper.Start(KillNextUpdate());
        }

        private static IEnumerator KillNextUpdate()
        {
            PositionLastFrame = PlayerControllerBInstance.gameplayCamera.transform.position;
            yield return null; //To fix the body spawning under the map, wait until the next update cycle
            PositionCurrentFrame = PlayerControllerBInstance.gameplayCamera.transform.position;
            RagdollVelocity = (PositionCurrentFrame - PositionLastFrame) * VelocityMultiplier; //The difference in position from these frames are insanely small, so it's multiplied
            RagdollVelocity.y *= 25 / VelocityMultiplier; //Reduce velocity in y-axis, the y-axis velocity will increase when VelocityMultiplier is under 25 (otherwise it would be negligible)

            PlayerControllerBInstance.KillPlayer(RagdollVelocity, true, (CauseOfDeath)ModSettings.DeathCause.Value, ModSettings.RagdollType.Value);
        }
    }
}