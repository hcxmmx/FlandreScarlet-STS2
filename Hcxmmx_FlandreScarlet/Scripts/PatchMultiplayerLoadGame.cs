#nullable enable
using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Hcxmmx.FlandreScarletMod.Scripts;

internal static class FlandreMultiplayerLoad
{
    internal const string ScenePath =
        "res://Hcxmmx_FlandreScarlet/Tscn/FlandreMultiplayerLoad.tscn";
    internal const string NodeName = "FlandreMultiplayerLoad";

    internal static PackedScene? Scene;
}

[HarmonyPatch]
internal static class NMultiplayerLoadGameScreenAfterStartPatch
{
    private static MethodBase TargetMethod()
    {
        // The game builds the local player's character-select background in this
        // private method after the multiplayer save has finished loading.
        return AccessTools.Method(
            typeof(NMultiplayerLoadGameScreen),
            "AfterMultiplayerStarted")!;
    }

    private static void Postfix(NMultiplayerLoadGameScreen __instance)
    {
        var bgContainer = Traverse.Create(__instance)
            .Field("_bgContainer")
            .GetValue<Control>();
        if (bgContainer == null)
        {
            GD.PrintErr("[FlandreScarlet] Multiplayer load background container was not found.");
            return;
        }

        // AfterMultiplayerStarted names this root from the saved local player's
        // character id. Its presence is therefore also our Ironclad guard.
        var officialRoot = bgContainer.GetNodeOrNull<Control>(
            FlandreCombat.TargetCharacterId + "_bg");
        if (officialRoot == null)
        {
            return;
        }

        foreach (var child in officialRoot.GetChildren())
        {
            if (child is CanvasItem canvasItem)
            {
                canvasItem.Hide();
            }
        }

        // Do not put our wallpaper under AnimatedBg/IRONCLAD_bg. The load screen
        // deliberately enlarges and offsets that container for the official
        // character-select art, which would crop and blur a complete 16:9 image.
        // Add it directly to the full-screen root, immediately above AnimatedBg
        // and below InfoPanel/the remaining UI instead.
        if (__instance.GetNodeOrNull(FlandreMultiplayerLoad.NodeName) == null)
        {
            var scene = FlandreMultiplayerLoad.Scene
                        ?? ResourceLoader.Load<PackedScene>(FlandreMultiplayerLoad.ScenePath);
            FlandreMultiplayerLoad.Scene = scene;
            if (scene == null)
            {
                GD.PrintErr(
                    $"[FlandreScarlet] Could not load scene: {FlandreMultiplayerLoad.ScenePath}");
                return;
            }

            var replacement = scene.Instantiate<Control>();
            replacement.Name = FlandreMultiplayerLoad.NodeName;
            __instance.AddChild(replacement);
            replacement.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            __instance.MoveChild(replacement, bgContainer.GetIndex() + 1);
            replacement.Show();
        }

        var nameLabel = Traverse.Create(__instance).Field("_name").GetValue();
        if (nameLabel != null)
        {
            Traverse.Create(nameLabel)
                .Method("SetTextAutoSize", new object[] { FlandreCharacterSelect.DisplayName })
                .GetValue();
        }

        GD.Print(
            $"[FlandreScarlet] Multiplayer load background injected. " +
            $"container size={bgContainer.Size}, pos={bgContainer.Position}, " +
            $"scale={bgContainer.Scale}; official root size={officialRoot.Size}, " +
            $"pos={officialRoot.Position}, scale={officialRoot.Scale}.");
    }
}
