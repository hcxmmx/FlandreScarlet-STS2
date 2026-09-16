#nullable enable
using System;
using Godot;

namespace Hcxmmx.FlandreScarletMod.Scripts;

public partial class FlandreAnimationTest : Node2D
{
    private FlandreController? _character;
    private OptionButton? _seriesSelector;
    private OptionButton? _variantSelector;
    private OptionButton? _emphasisSelector;
    private bool _sequenceRunning;

    public override void _Ready()
    {
        _character = GetNodeOrNull<FlandreController>("FlandreCharacter");
        if (_character == null)
        {
            GD.PrintErr("[FlandreScarlet] Test character not found.");
            return;
        }

        BuildControls();
        RefreshVariantSelector();
    }

    private void BuildControls()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var panel = new PanelContainer
        {
            Position = new Vector2(16f, 16f),
            CustomMinimumSize = new Vector2(310f, 0f),
        };
        canvas.AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        panel.AddChild(root);

        root.AddChild(new Label { Text = "Flandre Animation Test" });
        root.AddChild(new HSeparator());

        _seriesSelector = new OptionButton();
        _seriesSelector.AddItem("Light Attack");
        _seriesSelector.AddItem("Sword Attack");
        _seriesSelector.ItemSelected += _ => RefreshVariantSelector();
        root.AddChild(MakeLabeledRow("Series", _seriesSelector));

        _variantSelector = new OptionButton();
        root.AddChild(MakeLabeledRow("Variant", _variantSelector));

        _emphasisSelector = new OptionButton();
        _emphasisSelector.AddItem("Normal");
        _emphasisSelector.AddItem("Enhanced");
        _emphasisSelector.AddItem("Random 25%");
        root.AddChild(MakeLabeledRow("Strength", _emphasisSelector));

        AddButton(root, "Play Selected Variant", PlaySelectedVariant);
        AddButton(root, "Random Attack (Game Rules)", () => _character?.DebugPlayRandomAttack());
        AddButton(root, "Random Combo x3", PlayRandomCombo);
        AddButton(root, "Attack -> Hit Interrupt", PlayHitInterrupt);
        AddButton(root, "Heavy EX: Shatter", () => _character?.DebugPlayTrigger("HeavyShatter"));
        AddButton(root, "Heavy EX: Dash", () => _character?.DebugPlayTrigger("HeavyDash"));
        AddButton(root, "Heavy EX: Fourfold", () => _character?.DebugPlayTrigger("HeavyFourfold"));
        AddButton(root, "Heavy 03: Downward Arc", () => _character?.DebugPlayTrigger("HeavyAttack_03"));
        AddButton(root, "Heavy 05: Wide Arc", () => _character?.DebugPlayTrigger("HeavyAttack_05"));
        AddButton(root, "Heavy 06: Downward Crush", () => _character?.DebugPlayTrigger("HeavyAttack_06"));
        AddButton(root, "Heavy 07: Low Diagonal", () => _character?.DebugPlayTrigger("HeavyAttack_07"));
        AddButton(root, "Cast EX: Crystal Orb", () => _character?.DebugPlayTrigger("CastCrystalOrb"));
        AddButton(root, "Cast EX: Scarlet Collapse", () => _character?.DebugPlayTrigger("CastScarletCollapse"));
        AddButton(root, "Hit: Guard", () => _character?.DebugPlayTrigger("HitGuard"));
        AddButton(root, "Hit: Hat", () => _character?.DebugPlayTrigger("HitHat"));
        AddButton(root, "Hit: Crouch", () => _character?.DebugPlayTrigger("HitCrouch"));
        AddButton(root, "PowerUp EX: Red Moon", () => _character?.DebugPlayTrigger("PowerUpRedMoon"));
        AddButton(root, "PowerUp EX: Crystal Resonance", () => _character?.DebugPlayTrigger("PowerUpCrystalResonance"));
        AddButton(root, "Entrance: Scarlet Moon", () => _character?.DebugPlayTrigger("Intro"));
        AddButton(root, "Entrance: Crystal Awakening", () => _character?.DebugPlayTrigger("IntroCrystalAwakening"));
        AddButton(root, "Victory: Random", () => _character?.DebugPlayTrigger("Victory"));
        AddButton(root, "Victory: 495 Ripple", () => _character?.DebugPlayTrigger("Victory495"));
        AddButton(root, "Victory: Crystal Cheer", () => _character?.DebugPlayTrigger("VictoryCheer"));
        AddButton(root, "Dead: Ehehe", () => _character?.DebugPlayTrigger("DeadEhehe"));
        AddButton(root, "Dead: Hug Knees", () => _character?.DebugPlayTrigger("DeadHug"));

        root.AddChild(new HSeparator());
        var actionsFirstRow = new HBoxContainer();
        root.AddChild(actionsFirstRow);
        AddButton(actionsFirstRow, "Heavy", () => _character?.DebugPlayTrigger("heavyAttack"));
        AddButton(actionsFirstRow, "Cast", () => _character?.DebugPlayTrigger("Cast"));
        AddButton(actionsFirstRow, "PowerUp", () => _character?.DebugPlayTrigger("PowerUp"));

        var actionsSecondRow = new HBoxContainer();
        root.AddChild(actionsSecondRow);
        AddButton(actionsSecondRow, "Hit", () => _character?.DebugPlayTrigger("Hit"));
        AddButton(actionsSecondRow, "Dead", () => _character?.DebugPlayTrigger("Dead"));
        AddButton(actionsSecondRow, "Reset / Idle", () => _character?.DebugReset());

        root.AddChild(new Label
        {
            Text = "F6: run this scene\nCombo interval: 0.23 s",
            Modulate = new Color(0.75f, 0.75f, 0.75f),
        });
    }

    private static HBoxContainer MakeLabeledRow(string labelText, Control control)
    {
        var row = new HBoxContainer();
        var label = new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(72f, 0f),
        };
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(control);
        return row;
    }

    private static void AddButton(Node parent, string text, Action callback)
    {
        var button = new Button
        {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.Pressed += callback;
        parent.AddChild(button);
    }

    private void RefreshVariantSelector()
    {
        if (_character == null || _seriesSelector == null || _variantSelector == null)
        {
            return;
        }

        _variantSelector.Clear();
        bool swordSeries = _seriesSelector.Selected == 1;
        int count = _character.GetDebugAttackVariantCount(swordSeries);
        for (int index = 0; index < count; index++)
        {
            _variantSelector.AddItem($"{index + 1:00}");
        }
    }

    private void PlaySelectedVariant()
    {
        if (_character == null || _seriesSelector == null
            || _variantSelector == null || _emphasisSelector == null)
        {
            return;
        }

        _character.DebugPlayAttackVariant(
            _seriesSelector.Selected == 1,
            _variantSelector.Selected,
            _emphasisSelector.Selected);
    }

    private async void PlayRandomCombo()
    {
        if (_character == null || _sequenceRunning)
        {
            return;
        }

        _sequenceRunning = true;
        for (int strike = 0; strike < 3; strike++)
        {
            _character.DebugPlayRandomAttack();
            await ToSignal(GetTree().CreateTimer(0.23), SceneTreeTimer.SignalName.Timeout);
        }

        _sequenceRunning = false;
    }

    private async void PlayHitInterrupt()
    {
        if (_character == null || _sequenceRunning)
        {
            return;
        }

        _sequenceRunning = true;
        _character.DebugPlayRandomAttack();
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        _character.DebugPlayTrigger("Hit");
        _sequenceRunning = false;
    }
}
