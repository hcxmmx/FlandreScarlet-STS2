#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    internal static bool ScenePreloadRequested;
    internal static ulong ScenePreloadStartedAt;
    internal static bool EnableTriggerLogging = true;
    internal static readonly HashSet<FlandreController> ActiveControllers = new();

    internal static void RequestScenePreload()
    {
        if (Scene != null || ScenePreloadRequested)
        {
            return;
        }

        Error error = ResourceLoader.LoadThreadedRequest(
            ScenePath,
            "PackedScene",
            useSubThreads: true,
            ResourceLoader.CacheMode.Reuse);
        if (error != Error.Ok)
        {
            GD.PrintErr($"[FlandreScarlet] Could not request threaded combat preload: {error}.");
            return;
        }

        ScenePreloadRequested = true;
        ScenePreloadStartedAt = Time.GetTicksMsec();
        GD.Print("[FlandreScarlet] Threaded combat scene preload requested.");
    }

    internal static PackedScene? GetCombatScene()
    {
        if (Scene != null)
        {
            return Scene;
        }

        if (ScenePreloadRequested)
        {
            ResourceLoader.ThreadLoadStatus status =
                ResourceLoader.LoadThreadedGetStatus(ScenePath);
            ulong elapsed = Time.GetTicksMsec() - ScenePreloadStartedAt;
            GD.Print(
                $"[FlandreScarlet] Combat preload status at first use: {status} " +
                $"after {elapsed} ms.");

            if (status != ResourceLoader.ThreadLoadStatus.Failed
                && status != ResourceLoader.ThreadLoadStatus.InvalidResource)
            {
                // If loading is still in progress this waits for the same request;
                // it does not start a second synchronous load.
                Scene = ResourceLoader.LoadThreadedGet(ScenePath) as PackedScene;
            }
        }

        Scene ??= ResourceLoader.Load<PackedScene>(ScenePath);
        return Scene;
    }
}

[HarmonyPatch]
internal static class CombatManagerEndCombatPatch
{
    private static MethodBase TargetMethod()
    {
        // The stable build has EndCombatInternal(), while beta passes a turn
        // state. Select whichever signature the running game actually exposes.
        var methods = typeof(CombatManager)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "EndCombatInternal")
            .ToArray();
        return methods.SingleOrDefault(method => method.GetParameters().Length == 1)
            ?? methods.Single(method => method.GetParameters().Length == 0);
    }

    private static void Prefix()
    {
        GD.Print("[FlandreScarlet] Combat victory detected; requesting victory animation.");

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

        var scene = FlandreCombat.GetCombatScene();
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
