using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using MelonLoader;
using System;
using UnityEngine;

[assembly: MelonInfo(
    typeof(RoundSelector.RoundSelectorMod),
    RoundSelector.ModHelperData.Name,
    RoundSelector.ModHelperData.Version,
    RoundSelector.ModHelperData.RepoOwner
)]

[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]

namespace RoundSelector;

/// <summary>
/// Press J during a match, enter a round number,
/// and confirm to jump directly to that round.
/// </summary>
public sealed class RoundSelectorMod : BloonsTD6Mod
{
    // Used to configure the native popup's input field
    // after BTD6 creates it.
    private bool waitingForPopup;

    // Used to verify the round change on the following frame.
    private int roundToVerify = -1;
    private int verificationDelay;

    public override void OnApplicationStart()
    {
        ModHelper.Msg<RoundSelectorMod>(
            "Round Selector loaded. Press J during a match."
        );
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        InGame inGame = InGame.instance;

        // The mod can only operate during a live match.
        if (inGame?.bridge == null)
        {
            waitingForPopup = false;
            roundToVerify = -1;
            return;
        }

        // Press J to open the round-entry popup.
        if (Input.GetKeyDown(KeyCode.J))
        {
            OpenRoundPopup();
        }

        ConfigurePopupInput();

        // Verify the game bridge reports the requested round.
        VerifyRoundChange();
    }

    /// <summary>
    /// Opens BTD6's native text-entry popup.
    /// </summary>
    private void OpenRoundPopup()
    {
        InGame inGame = InGame.instance;

        if (inGame?.bridge == null)
        {
            return;
        }

        // GetCurrentRound is zero-based internally.
        int currentDisplayedRound =
            inGame.bridge.GetCurrentRound() + 1;

        Action<string> confirmAction = enteredText =>
        {
            JumpToRound(enteredText);
        };

        PopupScreen.instance.ShowSetNamePopup(
            "Round Selector",
            "Enter a round from 1 to 9999:",
            confirmAction,
            currentDisplayedRound.ToString()
        );

        // The TMP input field is created after the popup call,
        // so configure it during the following update.
        waitingForPopup = true;
    }

    /// <summary>
    /// Restricts the popup to numeric input.
    /// </summary>
    private void ConfigurePopupInput()
    {
        if (!waitingForPopup)
        {
            return;
        }

        var popup =
            PopupScreen.instance?.GetFirstActivePopup();

        if (popup == null)
        {
            return;
        }

        TMP_InputField input =
            popup.GetComponentInChildren<TMP_InputField>(true);

        if (input == null)
        {
            return;
        }

        input.characterValidation =
            TMP_InputField.CharacterValidation.Integer;

        input.characterLimit = 4;

        waitingForPopup = false;
    }

    /// <summary>
    /// Validates the entered value and changes the actual
    /// BTD6 bridge round.
    /// </summary>
    private void JumpToRound(string enteredText)
    {
        InGame inGame = InGame.instance;

        if (inGame?.bridge == null)
        {
            ModHelper.Error<RoundSelectorMod>(
                "No active match was found."
            );

            return;
        }

        string cleanedText =
            enteredText?.Trim() ?? string.Empty;

        if (!int.TryParse(
                cleanedText,
                out int targetRound
            ))
        {
            ModHelper.Error<RoundSelectorMod>(
                $"Invalid round number: \"{cleanedText}\""
            );

            return;
        }

        if (targetRound < 1 || targetRound > 9999)
        {
            ModHelper.Error<RoundSelectorMod>(
                "Round must be between 1 and 9999."
            );

            return;
        }

        try
        {
            /*
             * BTD6 uses zero-based round numbers internally:
             *
             * Displayed Round 1   = internal round 0
             * Displayed Round 100 = internal round 99
             * Displayed Round 420 = internal round 419
             *
             * Calling the bridge is important because it updates
             * the game's round state, rather than only changing
             * the map spawner.
             */
            inGame.bridge.SetRound(targetRound - 1);

            // Check the result again on the following frame.
            roundToVerify = targetRound;
            verificationDelay = 1;

            ModHelper.Msg<RoundSelectorMod>(
                $"Requested jump to Round {targetRound}."
            );
        }
        catch (Exception exception)
        {
            roundToVerify = -1;

            ModHelper.Error<RoundSelectorMod>(
                $"Round change failed: {exception}"
            );
        }
    }

    /// <summary>
    /// Confirms that the game now reports the selected round.
    /// If necessary, it applies the bridge change one more time.
    /// </summary>
    private void VerifyRoundChange()
    {
        if (roundToVerify < 1)
        {
            return;
        }

        if (verificationDelay > 0)
        {
            verificationDelay--;
            return;
        }

        InGame inGame = InGame.instance;

        if (inGame?.bridge == null)
        {
            roundToVerify = -1;
            return;
        }

        int requestedRound = roundToVerify;
        int actualRound =
            inGame.bridge.GetCurrentRound() + 1;

        if (actualRound != requestedRound)
        {
            // Retry directly through the game bridge.
            inGame.bridge.SetRound(requestedRound - 1);

            actualRound =
                inGame.bridge.GetCurrentRound() + 1;
        }

        if (actualRound == requestedRound)
        {
            ModHelper.Msg<RoundSelectorMod>(
                $"Successfully changed to Round {actualRound}."
            );
        }
        else
        {
            ModHelper.Error<RoundSelectorMod>(
                $"Round change failed. Requested {requestedRound}, " +
                $"but the game reports Round {actualRound}."
            );
        }

        roundToVerify = -1;
    }
}