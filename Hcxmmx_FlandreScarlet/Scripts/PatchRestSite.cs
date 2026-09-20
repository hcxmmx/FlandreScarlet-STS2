#nullable enable
using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace Hcxmmx.FlandreScarletMod.Scripts;

internal static class FlandreRestSite
{
    internal const string ScenePath = "res://Hcxmmx_FlandreScarlet/Tscn/FlandreRestSite.tscn";
    internal const string NodeName = "FlandreRestSite";
    internal static PackedScene? Scene;

    internal static bool IsTarget(NRestSiteCharacter character)
    {
        return string.Equals(
            character.Player.Character.Id.Entry,
            FlandreCombat.TargetCharacterId,
            StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
internal static class NRestSiteCharacterReadyPatch
{
    private static void Postfix(NRestSiteCharacter __instance)
    {
        if (!FlandreRestSite.IsTarget(__instance))
        {
            return;
        }

        foreach (var child in __instance.GetChildren())
        {
            if (child is CanvasItem canvasItem && child.GetClass() == "SpineSprite")
            {
                canvasItem.Hide();
            }
        }

        if (__instance.GetNodeOrNull<Node2D>(FlandreRestSite.NodeName) != null)
        {
            return;
        }

        var scene = FlandreRestSite.Scene
                    ?? ResourceLoader.Load<PackedScene>(FlandreRestSite.ScenePath);
        FlandreRestSite.Scene = scene;
        if (scene == null)
        {
            GD.PrintErr($"[FlandreScarlet] Could not load rest-site scene: {FlandreRestSite.ScenePath}");
            return;
        }

        var replacement = scene.Instantiate<Node2D>();
        replacement.Name = FlandreRestSite.NodeName;
        __instance.AddChild(replacement);

        // Keep the replacement in the original Spine render slot, below the
        // official hitbox, selection reticle and thought-bubble anchors.
        __instance.MoveChild(replacement, 1);
        GD.Print("[FlandreScarlet] Rest-site character injected.");
    }
}

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.FlipX))]
internal static class NRestSiteCharacterFlipXPatch
{
    private static void Postfix(NRestSiteCharacter __instance)
    {
        if (!FlandreRestSite.IsTarget(__instance))
        {
            return;
        }

        var replacement = __instance.GetNodeOrNull<Node2D>(FlandreRestSite.NodeName);
        if (replacement == null)
        {
            return;
        }

        var scale = replacement.Scale;
        scale.X = -scale.X;
        replacement.Scale = scale;
    }
}
