#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Models.Potions;

namespace Hcxmmx.FlandreScarletMod.Scripts;

internal static class FlandreMerchant
{
    internal const string ScenePath = "res://Hcxmmx_FlandreScarlet/Tscn/FlandreMerchant.tscn";
    internal const string NodeNamePrefix = "FlandreMerchant";
    internal static PackedScene? Scene;
}

internal static class FlandreFakeMerchant
{
    internal const string ScenePath = "res://Hcxmmx_FlandreScarlet/Tscn/FlandreFakeMerchant.tscn";
    internal const string NodeNamePrefix = "FlandreFakeMerchant";
    internal static PackedScene? Scene;
}

[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))]
internal static class NMerchantRoomReadyPatch
{
    private static void Postfix(NMerchantRoom __instance)
    {
        var players = Traverse.Create(__instance)
            .Field("_players")
            .GetValue<List<Player>>();
        var visuals = __instance.PlayerVisuals;

        if (players == null || players.Count != visuals.Count)
        {
            GD.PrintErr("[FlandreScarlet] Merchant players and visuals did not match.");
            return;
        }

        var scene = FlandreMerchant.Scene
                    ?? ResourceLoader.Load<PackedScene>(FlandreMerchant.ScenePath);
        FlandreMerchant.Scene = scene;
        if (scene == null)
        {
            GD.PrintErr($"[FlandreScarlet] Could not load merchant scene: {FlandreMerchant.ScenePath}");
            return;
        }

        for (var i = 0; i < players.Count; i++)
        {
            if (!string.Equals(
                    players[i].Character.Id.Entry,
                    FlandreCombat.TargetCharacterId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var original = visuals[i];
            var parent = original.GetParent();
            if (parent == null || parent.GetNodeOrNull($"{FlandreMerchant.NodeNamePrefix}_{i}") != null)
            {
                continue;
            }

            original.Hide();

            var replacement = scene.Instantiate<FlandreMerchantController>();
            replacement.Name = $"{FlandreMerchant.NodeNamePrefix}_{i}";
            replacement.Position = original.Position;
            replacement.Rotation = original.Rotation;
            replacement.Modulate = original.Modulate;
            replacement.ZIndex = original.ZIndex;
            replacement.Initialize(__instance.Inventory.Inventory);
            parent.AddChild(replacement);
            parent.MoveChild(replacement, original.GetIndex() + 1);

            GD.Print($"[FlandreScarlet] Merchant character injected for player slot {i}.");
        }
    }
}

[HarmonyPatch(typeof(NFakeMerchant), nameof(NFakeMerchant._Ready))]
internal static class NFakeMerchantReadyPatch
{
    private static void Postfix(NFakeMerchant __instance)
    {
        var players = Traverse.Create(__instance)
            .Field("_players")
            .GetValue<List<Player>>();
        var characterContainer = __instance.GetNodeOrNull<Control>("%CharacterContainer");
        if (players == null || characterContainer == null)
        {
            return;
        }

        // NFakeMerchant inserts every newly created player visual at child index 0.
        // Reverse that final node order to recover the already-sorted player order.
        var visuals = characterContainer.GetChildren()
            .OfType<NCreatureVisuals>()
            .Reverse()
            .ToArray();
        if (visuals.Length != players.Count)
        {
            GD.PrintErr("[FlandreScarlet] Fake-merchant players and visuals did not match.");
            return;
        }

        var scene = FlandreFakeMerchant.Scene
                    ?? ResourceLoader.Load<PackedScene>(FlandreFakeMerchant.ScenePath);
        FlandreFakeMerchant.Scene = scene;
        if (scene == null)
        {
            GD.PrintErr($"[FlandreScarlet] Could not load fake-merchant scene: {FlandreFakeMerchant.ScenePath}");
            return;
        }

        for (var i = 0; i < players.Count; i++)
        {
            if (!string.Equals(
                    players[i].Character.Id.Entry,
                    FlandreCombat.TargetCharacterId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var original = visuals[i];
            if (characterContainer.GetNodeOrNull($"{FlandreFakeMerchant.NodeNamePrefix}_{i}") != null)
            {
                continue;
            }

            original.Hide();

            var replacement = scene.Instantiate<FlandreFakeMerchantController>();
            replacement.Name = $"{FlandreFakeMerchant.NodeNamePrefix}_{i}";
            replacement.Position = original.Position;
            replacement.Rotation = original.Rotation;
            replacement.Modulate = original.Modulate;
            replacement.ZIndex = original.ZIndex;
            replacement.Initialize(players[i].Potions.Any(potion => potion is FoulPotion));
            characterContainer.AddChild(replacement);

            GD.Print($"[FlandreScarlet] Fake-merchant character injected for player slot {i}.");
        }
    }
}
