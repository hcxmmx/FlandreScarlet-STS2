#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Hcxmmx.FlandreScarletMod.Scripts;

internal static class FlandreCombat
{
    internal const string TargetCharacterId = "IRONCLAD";
    internal const string ScenePath = "res://Hcxmmx_FlandreScarlet/Tscn/FlandreCharacter.tscn";
    internal const string NodeName = "FlandreCharacter";

    internal static PackedScene? Scene;
    internal static bool EnableTriggerLogging = true;
    internal static readonly HashSet<FlandreController> ActiveControllers = new();
}

[HarmonyPatch(typeof(CombatManager), "EndCombatInternal", new Type[0])]
internal static class CombatManagerEndCombatPatch
{
    private static void Prefix()
    {
        foreach (var controller in new List<FlandreController>(FlandreCombat.ActiveControllers))
        {
            if (GodotObject.IsInstanceValid(controller))
            {
                controller.RequestVictory();
            }
            else
            {
                FlandreCombat.ActiveControllers.Remove(controller);
            }
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class NCreatureReadyPatch
{
    private static void Postfix(NCreature __instance)
    {
        var player = __instance.Entity?.Player;
        if (player == null || !string.Equals(
                player.Character?.Id?.Entry,
                FlandreCombat.TargetCharacterId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var visuals = __instance.Visuals;
        if (visuals == null || visuals.GetNodeOrNull(FlandreCombat.NodeName) != null)
        {
            return;
        }

        var scene = FlandreCombat.Scene ?? ResourceLoader.Load<PackedScene>(FlandreCombat.ScenePath);
        FlandreCombat.Scene = scene;
        if (scene == null)
        {
            GD.PrintErr($"[FlandreScarlet] Could not load scene: {FlandreCombat.ScenePath}");
            return;
        }

        var originalBody = visuals.GetNodeOrNull<Node2D>("%Visuals");
        if (originalBody != null)
        {
            // 形态特效仍会从原 Spine 读取骨骼坐标。真正 Hide() 可能让
            // Spine 停止更新并令跟随特效落到全局原点，因此只隐藏渲染。
            originalBody.Show();
            var transparent = originalBody.Modulate;
            transparent.A = 0.0f;
            originalBody.Modulate = transparent;
        }

        var flandre = scene.Instantiate<FlandreController>();
        flandre.Name = FlandreCombat.NodeName;
        visuals.AddChild(flandre);
        flandre.Initialize(originalBody);

        GD.Print("[FlandreScarlet] Replaced Ironclad combat visuals.");
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class NCreatureAnimationTriggerPatch
{
    private static void Postfix(NCreature __instance, string trigger)
    {
        var player = __instance.Entity?.Player;
        if (player == null || !string.Equals(
                player.Character?.Id?.Entry,
                FlandreCombat.TargetCharacterId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (FlandreCombat.EnableTriggerLogging)
        {
            GD.Print($"[FlandreScarlet] Animation trigger: {trigger}");
        }

        var controller = __instance.Visuals?.GetNodeOrNull<FlandreController>(FlandreCombat.NodeName);
        controller?.HandleGameTrigger(trigger);
    }
}
