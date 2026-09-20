#nullable enable
using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Hcxmmx.FlandreScarletMod.Scripts;

internal static class FlandreCharacterSelect
{
    internal const string ScenePath = "res://Hcxmmx_FlandreScarlet/Tscn/FlandreCharacterSelect.tscn";
    internal const string NodeName = "FlandreCharacterSelect";
    internal const string DisplayName = "芙兰朵露";
    internal const string DisplayDescription =
        "红魔馆中鲜少外出的吸血鬼妹妹。\n怀着纯粹的好奇心，以足以摧毁一切的力量踏入高塔。";
    internal static PackedScene? Scene;
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class NCharacterSelectScreenSelectCharacterPatch
{
    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        if (!string.Equals(
                characterModel.Id.Entry,
                FlandreCombat.TargetCharacterId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyCharacterText(__instance);

        var bgContainer = Traverse.Create(__instance).Field("_bgContainer").GetValue<Control>();
        if (bgContainer == null)
        {
            GD.PrintErr("[FlandreScarlet] Character select AnimatedBg was not found.");
            return;
        }

        // SelectCharacter has already created the official IroncladBg here. Keep that
        // root because its offsets/pivot are the missing part of the game's layout.
        var officialRoot = bgContainer.GetNodeOrNull<Control>(characterModel.Id.Entry + "_bg");
        if (officialRoot == null)
        {
            GD.PrintErr("[FlandreScarlet] Official Ironclad character-select root was not found.");
            return;
        }

        foreach (var child in officialRoot.GetChildren())
        {
            if (child is CanvasItem canvasItem)
            {
                canvasItem.Hide();
            }
        }

        if (officialRoot.GetNodeOrNull(FlandreCharacterSelect.NodeName) != null)
        {
            return;
        }

        var scene = FlandreCharacterSelect.Scene
                    ?? ResourceLoader.Load<PackedScene>(FlandreCharacterSelect.ScenePath);
        FlandreCharacterSelect.Scene = scene;
        if (scene == null)
        {
            GD.PrintErr($"[FlandreScarlet] Could not load scene: {FlandreCharacterSelect.ScenePath}");
            return;
        }

        var replacement = scene.Instantiate<Control>();
        replacement.Name = FlandreCharacterSelect.NodeName;
        officialRoot.AddChild(replacement);
        replacement.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        replacement.Show();

        GD.Print($"[FlandreScarlet] Character select injected. " +
                 $"AnimatedBg size={bgContainer.Size}, pos={bgContainer.Position}, scale={bgContainer.Scale}; " +
                 $"official root size={officialRoot.Size}, pos={officialRoot.Position}, scale={officialRoot.Scale}.");
    }

    private static void ApplyCharacterText(NCharacterSelectScreen screen)
    {
        var nameLabel = Traverse.Create(screen).Field("_name").GetValue();
        if (nameLabel != null)
        {
            Traverse.Create(nameLabel)
                .Method("SetTextAutoSize", new object[] { FlandreCharacterSelect.DisplayName })
                .GetValue();
        }

        var descriptionLabel = Traverse.Create(screen)
            .Field("_description")
            .GetValue<RichTextLabel>();
        if (descriptionLabel != null)
        {
            descriptionLabel.Text = FlandreCharacterSelect.DisplayDescription;
        }
    }
}
