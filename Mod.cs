using System;
using System.Linq;
using System.Globalization;
using UnityEngine;
using TMPro;
using MelonLoader;
using HarmonyLib;
using Core.Services;
using Managers.Server.Services;
using Players.Services;
using Players.Interface.Model;
using Ships.Services;
using Ships.Interface.Settings;
using Ships.Interface.Model.State;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Mathos.Parser;

namespace PiIsDelicious;


public class Mod : MelonMod
{
    public static bool InitializationWasSuccessfull { get; private set; }


    public override void OnLateInitializeMelon()
    {
        InitializationWasSuccessfull = TryModifyTextInput();

        if (!InitializationWasSuccessfull)
        {
            // try to tell the user what's going on
            LoggerInstance.Warning("This mod's features won't be available (the game version is probably too new for this mod)");
        }
    }


    // modifies the input field on the constant part so we can enter other charcaters
    private bool TryModifyTextInput()
    {
        var constantPart = Resources.FindObjectsOfTypeAll<PartSettings>()
            .FirstOrDefault(p => p.name == "Constant");

        if (constantPart is null)
        {
            LoggerInstance.Error("Couldn't find 'Constant' PartSettings");
            return false;
        }
        LoggerInstance.Msg("Found 'Constant' PartSettings");


        var input = constantPart.part.gameObject.GetComponentInChildren<TMP_InputField>();

        if (input is null)
        {
            LoggerInstance.Error("Couldn't find TMP_InputField Component of 'Constant'");
            return false;
        }
        LoggerInstance.Msg("Found TMP_InputField Component of 'Constant'");


        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterValidation = TMP_InputField.CharacterValidation.None; 
        LoggerInstance.Msg("ModifyTextInput was successfull'");

        return true;
    }
}


public static class Evaluator
{
    private static readonly MathParser _parser = new(loadPreDefinedVariables: false); // not fond of their other pi variables

    static Evaluator()
    {            
        _parser.LocalVariables.Add("pi", 3.14159265358979m);
        _parser.LocalVariables.Add("tau", 6.28318530717959m);
        _parser.LocalVariables.Add("e", 2.71828182845904m);
        _parser.LocalVariables.Add("phi", 1.6180339887499m);
    }

    public static string Process(string expression)
    {
        decimal result = 0;

        try
        {
            result = _parser.Parse(expression);
        }
        catch (Exception e)
        {
            MelonLogger.Msg($"Couldn't evaluate '{expression}' because '{e.Message}'");
        }

        return ToNumberString(result);
    }

    private static string ToNumberString(object obj) 
    {
        return Convert.ToDouble(obj).ToString("G", CultureInfo.InvariantCulture);
    }
}


// patch the method that handles text changes 
// (it also handles parts like beacons so we gotta test what the player is doing)
[HarmonyPatch(typeof(ShipInteractionService), nameof(ShipInteractionService.ChangeTextInputVal), [typeof(string)])]
internal static class Patch
{
    private static void Prefix(ref string beaconName)
    {
        // Something is broken so don't utilize the patch
        if (!Mod.InitializationWasSuccessfull) return;


        var players = ServiceLocator.GetService<PlayersClientTracker>();
        var ships = ServiceLocator.GetService<ShipsClientTracker>();

        if (!players.TryGetLocalPlayerState(out PlayerState playerState)) return;
        if (!ships.TryGetShip(playerState.EntityId, out IShipStateRead ship)) return;
        if (!ship.TryGetStatefulPart(playerState.PartId, out StatefulPart part)) return;
        
        // we only want to process the string if it's actually for a constant (it could be a beacon or something similar)
        if (part.State is ConstantState)
        {
            beaconName = Evaluator.Process(beaconName);
        }
    }
}