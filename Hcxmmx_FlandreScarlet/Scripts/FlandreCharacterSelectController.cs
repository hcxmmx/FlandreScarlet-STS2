#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Hcxmmx.FlandreScarletMod.Scripts;

public partial class FlandreCharacterSelectController : Control
{
    private static readonly string[] HeadPatTexturePaths =
    {
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_headpat_01.png",
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_headpat_02.png",
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_headpat_03.png"
    };

    private static readonly string[] CrystalWarningTexturePaths =
    {
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_crystal_warning_01.png",
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_crystal_warning_02.png"
    };

    private static readonly string[] CrystalSpecialTexturePaths =
    {
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_crystal_special_01.png",
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_crystal_special_02.png"
    };

    private const string HighFiveTexturePath =
        "res://Hcxmmx_FlandreScarlet/Assets/CharacterSelect/select_highfive_reaction.png";

    private static readonly Vector2 HighFiveHandTarget = new(-143f, -325f);
    private static readonly Vector2 HighFiveContactPoint = new(-144f, -340f);

    private static readonly Vector2[] HeadPatContactPositions =
    {
        new(35f, -380f),
        new(20f, -390f),
        new(45f, -395f)
    };

    // Local to CharacterPivot: four clear crystals on each side of the normal pose.
    private static readonly Vector2[] CrystalPositions =
    {
        new(-277f, -123f),
        new(-215f, -179f),
        new(-181f, -237f),
        new(-138f, -185f),
        new(179f, -222f),
        new(222f, -185f),
        new(259f, -135f),
        new(290f, -73f)
    };

    private static readonly Color[] CrystalColors =
    {
        new(1f, 0.12f, 0.22f),
        new(1f, 0.67f, 0.12f),
        new(0.24f, 1f, 0.32f),
        new(0.18f, 0.55f, 1f),
        new(1f, 0.72f, 0.16f),
        new(0.22f, 1f, 0.36f),
        new(0.16f, 0.58f, 1f),
        new(0.66f, 0.20f, 1f)
    };

    private readonly List<int> _headPatBag = new();
    private readonly List<int> _warningBag = new();
    private readonly List<int> _specialBag = new();
    private readonly Texture2D[] _headPatTextures = new Texture2D[HeadPatTexturePaths.Length];
    private readonly Texture2D[] _warningTextures = new Texture2D[CrystalWarningTexturePaths.Length];
    private readonly Texture2D[] _specialTextures = new Texture2D[CrystalSpecialTexturePaths.Length];
    private readonly Sprite2D[] _crystalLights = new Sprite2D[CrystalPositions.Length];
    private readonly Button[] _crystalButtons = new Button[CrystalPositions.Length];
    private readonly bool[] _crystalLit = new bool[CrystalPositions.Length];
    private readonly RandomNumberGenerator _rng = new();
    private Texture2D _highFiveTexture = null!;

    private Sprite2D _idleCharacter = null!;
    private Sprite2D _idleGlow = null!;
    private Sprite2D _reactionCharacter = null!;
    private Sprite2D _reactionGlow = null!;
    private Node2D _reactionMotion = null!;
    private Node2D _handPivot = null!;
    private Sprite2D _hand = null!;
    private Node2D _crystalLightsRoot = null!;
    private GpuParticles2D _crystalBurst = null!;
    private Node2D _highFiveHandPivot = null!;
    private Sprite2D _highFiveHand = null!;
    private Sprite2D _highFiveImpact = null!;
    private Button _headPatButton = null!;
    private Button _highFiveButton = null!;
    private bool _isReacting;
    private bool _sceneAlive = true;
    private int _crystalResetVersion;
    private int _warningCount;
    private int _lastHeadPat = -1;
    private int _lastWarning = -1;
    private int _lastSpecial = -1;

    public override void _Ready()
    {
        _sceneAlive = true;
        _rng.Randomize();
        _reactionMotion = GetNode<Node2D>("CharacterPivot/ReactionMotion");
        _idleGlow = GetNode<Sprite2D>("CharacterPivot/ReactionMotion/Glow");
        _idleCharacter = GetNode<Sprite2D>("CharacterPivot/ReactionMotion/Character");
        _reactionGlow = GetNode<Sprite2D>("CharacterPivot/ReactionMotion/ReactionGlow");
        _reactionCharacter = GetNode<Sprite2D>("CharacterPivot/ReactionMotion/ReactionCharacter");
        _handPivot = GetNode<Node2D>("CharacterPivot/HandPivot");
        _hand = GetNode<Sprite2D>("CharacterPivot/HandPivot/Hand");
        _crystalLightsRoot = GetNode<Node2D>("CharacterPivot/CrystalLights");
        _crystalBurst = GetNode<GpuParticles2D>("CharacterPivot/CrystalBurst");
        _highFiveHandPivot = GetNode<Node2D>("CharacterPivot/HighFiveHandPivot");
        _highFiveHand = GetNode<Sprite2D>("CharacterPivot/HighFiveHandPivot/Hand");
        _highFiveImpact = GetNode<Sprite2D>("CharacterPivot/HighFiveImpact");
        _headPatButton = GetNode<Button>("InteractionZones/HeadPatButton");
        _highFiveButton = GetNode<Button>("InteractionZones/HighFiveButton");

        LoadTextures(HeadPatTexturePaths, _headPatTextures);
        LoadTextures(CrystalWarningTexturePaths, _warningTextures);
        LoadTextures(CrystalSpecialTexturePaths, _specialTextures);
        _highFiveTexture = ResourceLoader.Load<Texture2D>(HighFiveTexturePath);

        _headPatButton.Pressed += OnHeadPatPressed;
        _highFiveButton.Pressed += OnHighFivePressed;
        for (var i = 0; i < CrystalPositions.Length; i++)
        {
            var index = i;
            _crystalLights[i] = GetNode<Sprite2D>($"CharacterPivot/CrystalLights/Light{i + 1}");
            _crystalButtons[i] = GetNode<Button>($"InteractionZones/CrystalButton{i + 1}");
            _crystalButtons[i].Pressed += () => OnCrystalPressed(index);
        }
    }

    public override void _ExitTree()
    {
        // Timers and signal awaiters may finish after the selection screen has
        // already switched character or started the run. They must not touch the
        // freed sprites/buttons when they resume.
        _sceneAlive = false;
        _crystalResetVersion++;
    }

    private static void LoadTextures(string[] paths, Texture2D[] destination)
    {
        for (var i = 0; i < paths.Length; i++)
        {
            destination[i] = ResourceLoader.Load<Texture2D>(paths[i]);
        }
    }

    private void OnHeadPatPressed()
    {
        if (!_isReacting)
        {
            PlayHeadPat();
        }
    }

    private void OnCrystalPressed(int index)
    {
        if (!_isReacting)
        {
            PlayCrystalClick(index);
        }
    }

    private void OnHighFivePressed()
    {
        if (!_isReacting)
        {
            PlayHighFive();
        }
    }

    private async void PlayHighFive()
    {
        _isReacting = true;
        _crystalResetVersion++;
        SetInteractionEnabled(false);
        PrepareReaction(_highFiveTexture);

        _highFiveHand.Visible = true;
        _highFiveHand.Modulate = new Color(1f, 1f, 1f, 0f);
        _highFiveHandPivot.Position = HighFiveHandTarget + new Vector2(105f, 315f);
        _highFiveHandPivot.Scale = new Vector2(0.84f, 0.84f);
        _highFiveHandPivot.Rotation = 0.035f;

        var enter = CreateTween().SetParallel(true);
        enter.TweenProperty(_idleCharacter, "modulate:a", 0f, 0.10f);
        enter.TweenProperty(_reactionCharacter, "modulate:a", 1f, 0.13f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        enter.TweenProperty(_reactionGlow, "modulate:a", 0.14f, 0.15f);
        enter.TweenProperty(_crystalLightsRoot, "modulate:a", 0f, 0.10f);
        enter.TweenProperty(_highFiveHand, "modulate:a", 1f, 0.09f);
        enter.TweenProperty(_highFiveHandPivot, "position", HighFiveHandTarget, 0.18f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        enter.TweenProperty(_highFiveHandPivot, "scale", Vector2.One, 0.18f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        enter.TweenProperty(_highFiveHandPivot, "rotation", 0f, 0.18f);
        await ToSignal(enter, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        _idleGlow.Visible = false;
        _crystalLightsRoot.Hide();
        _highFiveImpact.Visible = true;
        _highFiveImpact.Scale = new Vector2(0.25f, 0.25f);
        _highFiveImpact.Modulate = new Color(1f, 0.72f, 0.88f, 1f);
        _crystalBurst.Position = HighFiveContactPoint;
        _crystalBurst.Modulate = new Color(1f, 0.42f, 0.68f, 0.95f);
        _crystalBurst.Restart();
        _crystalBurst.Emitting = true;

        var impact = CreateTween();
        impact.SetParallel(true);
        impact.TweenProperty(_highFiveImpact, "scale", new Vector2(1.75f, 1.75f), 0.09f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        impact.TweenProperty(_reactionMotion, "position", new Vector2(9f, 7f), 0.09f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        impact.TweenProperty(_reactionMotion, "rotation", 0.012f, 0.09f);
        impact.TweenProperty(_reactionMotion, "scale", new Vector2(1.025f, 0.985f), 0.09f);
        impact.TweenProperty(_highFiveHandPivot, "position", HighFiveHandTarget + new Vector2(8f, 15f), 0.09f);
        impact.Chain().SetParallel(true);
        impact.TweenProperty(_highFiveImpact, "scale", new Vector2(2.45f, 2.45f), 0.17f);
        impact.TweenProperty(_highFiveImpact, "modulate:a", 0f, 0.17f);
        impact.TweenProperty(_reactionMotion, "position", Vector2.Zero, 0.24f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        impact.TweenProperty(_reactionMotion, "rotation", 0f, 0.24f);
        impact.TweenProperty(_reactionMotion, "scale", Vector2.One, 0.24f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        impact.TweenProperty(_highFiveHandPivot, "position", HighFiveHandTarget, 0.14f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await ToSignal(impact, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.34), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive)
        {
            return;
        }

        var leave = CreateTween().SetParallel(true);
        leave.TweenProperty(_highFiveHandPivot, "position", HighFiveHandTarget + new Vector2(115f, 325f), 0.18f)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        leave.TweenProperty(_highFiveHand, "modulate:a", 0f, 0.14f);
        leave.TweenProperty(_reactionCharacter, "modulate:a", 0f, 0.15f);
        leave.TweenProperty(_reactionGlow, "modulate:a", 0f, 0.15f);
        leave.TweenProperty(_idleCharacter, "modulate:a", 1f, 0.17f);
        await ToSignal(leave, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        _highFiveHand.Visible = false;
        _highFiveImpact.Visible = false;
        _highFiveHandPivot.Scale = Vector2.One;
        FinishReactionVisuals();
        _crystalLightsRoot.Modulate = Colors.White;
        _crystalLightsRoot.Show();
        await ToSignal(GetTree().CreateTimer(0.22), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive)
        {
            return;
        }
        FinishInteraction();
    }

    private async void PlayCrystalClick(int index)
    {
        _isReacting = true;
        _crystalResetVersion++;
        SetInteractionEnabled(false);
        var wasAlreadyLit = _crystalLit[index];
        _crystalLit[index] = true;

        EmitCrystalBurst(index);
        await AnimateCrystalPulse(index, wasAlreadyLit);
        if (!_sceneAlive)
        {
            return;
        }

        if (wasAlreadyLit)
        {
            FinishInteraction();
            return;
        }

        var litCount = CountLitCrystals();
        if (litCount == _crystalLit.Length)
        {
            await PlayCrystalSpecial();
            if (!_sceneAlive)
            {
                return;
            }
            FinishInteraction();
            return;
        }

        if (_rng.Randf() < GetDiscoveryChance(litCount))
        {
            await PlayCrystalWarning(index);
            if (!_sceneAlive)
            {
                return;
            }
        }

        FinishInteraction();
    }

    private async Task AnimateCrystalPulse(int index, bool alreadyLit)
    {
        var light = _crystalLights[index];
        var color = CrystalColors[index];
        light.Visible = true;
        if (!alreadyLit)
        {
            light.Scale = new Vector2(0.45f, 0.45f);
            light.Modulate = new Color(color.R, color.G, color.B, 0f);
        }

        var pulse = CreateTween();
        pulse.SetParallel(true);
        pulse.TweenProperty(light, "scale", new Vector2(1.48f, 1.48f), 0.13f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        pulse.TweenProperty(light, "modulate:a", 1f, 0.10f);
        pulse.Chain().SetParallel(true);
        pulse.TweenProperty(light, "scale", Vector2.One, 0.20f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);
        pulse.TweenProperty(light, "modulate:a", 0.58f, 0.18f);
        await ToSignal(pulse, Tween.SignalName.Finished);
    }

    private void EmitCrystalBurst(int index)
    {
        var color = CrystalColors[index];
        _crystalBurst.Position = CrystalPositions[index];
        _crystalBurst.Modulate = new Color(color.R, color.G, color.B, 0.92f);
        _crystalBurst.Restart();
        _crystalBurst.Emitting = true;
    }

    private async Task PlayCrystalWarning(int clickedIndex)
    {
        var variant = DrawVariant(_warningBag, _warningTextures.Length, ref _lastWarning);
        PrepareReaction(_warningTextures[variant]);

        var enter = CreateTween().SetParallel(true);
        enter.TweenProperty(_idleCharacter, "modulate:a", 0f, 0.11f);
        enter.TweenProperty(_reactionCharacter, "modulate:a", 1f, 0.13f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        enter.TweenProperty(_reactionGlow, "modulate:a", 0.15f, 0.15f);
        enter.TweenProperty(_crystalLightsRoot, "modulate:a", 0f, 0.09f);
        enter.TweenProperty(_reactionMotion, "scale", new Vector2(1.022f, 1.022f), 0.18f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await ToSignal(enter, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        _idleGlow.Visible = false;
        _crystalLightsRoot.Hide();
        await ToSignal(GetTree().CreateTimer(0.78), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive)
        {
            return;
        }

        var leave = CreateTween().SetParallel(true);
        leave.TweenProperty(_reactionCharacter, "modulate:a", 0f, 0.14f);
        leave.TweenProperty(_reactionGlow, "modulate:a", 0f, 0.14f);
        leave.TweenProperty(_idleCharacter, "modulate:a", 1f, 0.16f);
        leave.TweenProperty(_reactionMotion, "scale", Vector2.One, 0.16f);
        await ToSignal(leave, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        FinishReactionVisuals();
        _crystalLightsRoot.Modulate = Colors.White;
        _crystalLightsRoot.Show();

        var toExtinguish = new List<int> { clickedIndex };
        _warningCount++;
        if (_warningCount > 1)
        {
            var other = PickRandomLitCrystal(clickedIndex);
            if (other >= 0)
            {
                toExtinguish.Add(other);
            }
        }

        foreach (var index in toExtinguish)
        {
            await ExtinguishCrystal(index);
            if (!_sceneAlive)
            {
                return;
            }
            await ToSignal(GetTree().CreateTimer(0.07), SceneTreeTimer.SignalName.Timeout);
            if (!_sceneAlive)
            {
                return;
            }
        }
    }

    private async Task PlayCrystalSpecial()
    {
        var resonance = CreateTween().SetParallel(true);
        foreach (var light in _crystalLights)
        {
            resonance.TweenProperty(light, "scale", new Vector2(1.65f, 1.65f), 0.22f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            resonance.TweenProperty(light, "modulate:a", 1f, 0.16f);
        }
        await ToSignal(resonance, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        var variant = DrawVariant(_specialBag, _specialTextures.Length, ref _lastSpecial);
        PrepareReaction(_specialTextures[variant]);

        var enter = CreateTween().SetParallel(true);
        enter.TweenProperty(_idleCharacter, "modulate:a", 0f, 0.13f);
        enter.TweenProperty(_reactionCharacter, "modulate:a", 1f, 0.20f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        enter.TweenProperty(_reactionGlow, "modulate:a", 0.22f, 0.24f);
        enter.TweenProperty(_crystalLightsRoot, "modulate:a", 0f, 0.12f);
        enter.TweenProperty(_reactionMotion, "scale", new Vector2(1.035f, 1.035f), 0.32f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await ToSignal(enter, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        _idleGlow.Visible = false;
        _crystalLightsRoot.Hide();
        await ToSignal(GetTree().CreateTimer(1.65), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive)
        {
            return;
        }

        var leave = CreateTween().SetParallel(true);
        leave.TweenProperty(_reactionCharacter, "modulate:a", 0f, 0.20f);
        leave.TweenProperty(_reactionGlow, "modulate:a", 0f, 0.20f);
        leave.TweenProperty(_idleCharacter, "modulate:a", 1f, 0.22f);
        leave.TweenProperty(_reactionMotion, "scale", Vector2.One, 0.22f);
        await ToSignal(leave, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        FinishReactionVisuals();
        Array.Fill(_crystalLit, false);
        foreach (var light in _crystalLights)
        {
            light.Visible = false;
            light.Scale = Vector2.One;
        }
        _crystalLightsRoot.Modulate = Colors.White;
        _crystalLightsRoot.Show();
        _warningCount = 0;
    }

    private void PrepareReaction(Texture2D texture)
    {
        _reactionCharacter.Texture = texture;
        _reactionGlow.Texture = texture;
        _reactionMotion.Position = Vector2.Zero;
        _reactionMotion.Rotation = 0f;
        _reactionMotion.Scale = Vector2.One;
        _reactionCharacter.Visible = true;
        _reactionGlow.Visible = true;
        _reactionCharacter.Modulate = new Color(1f, 1f, 1f, 0f);
        _reactionGlow.Modulate = new Color(1f, 0.32f, 0.62f, 0f);
    }

    private void FinishReactionVisuals()
    {
        _reactionCharacter.Visible = false;
        _reactionGlow.Visible = false;
        _idleGlow.Visible = true;
        _reactionMotion.Position = Vector2.Zero;
        _reactionMotion.Rotation = 0f;
        _reactionMotion.Scale = Vector2.One;
    }

    private async Task ExtinguishCrystal(int index)
    {
        if (!_crystalLit[index])
        {
            return;
        }

        _crystalLit[index] = false;
        var light = _crystalLights[index];
        var extinguish = CreateTween().SetParallel(true);
        extinguish.TweenProperty(light, "scale", new Vector2(0.45f, 0.45f), 0.18f)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        extinguish.TweenProperty(light, "modulate:a", 0f, 0.16f);
        await ToSignal(extinguish, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }
        light.Visible = false;
        light.Scale = Vector2.One;
    }

    private int PickRandomLitCrystal(int excludedIndex)
    {
        var candidates = new List<int>();
        for (var i = 0; i < _crystalLit.Length; i++)
        {
            if (_crystalLit[i] && i != excludedIndex)
            {
                candidates.Add(i);
            }
        }

        return candidates.Count == 0 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
    }

    private int CountLitCrystals()
    {
        var count = 0;
        foreach (var lit in _crystalLit)
        {
            if (lit)
            {
                count++;
            }
        }
        return count;
    }

    private static float GetDiscoveryChance(int litCount)
    {
        return litCount switch
        {
            <= 2 => 0f,
            3 => 0.15f,
            4 => 0.25f,
            5 => 0.35f,
            6 => 0.45f,
            7 => 0.60f,
            _ => 0f
        };
    }

    private async void PlayHeadPat()
    {
        _isReacting = true;
        _crystalResetVersion++;
        SetInteractionEnabled(false);

        var variant = DrawVariant(_headPatBag, _headPatTextures.Length, ref _lastHeadPat);
        var contact = HeadPatContactPositions[variant];
        PrepareReaction(_headPatTextures[variant]);
        _hand.Visible = true;
        _hand.Modulate = new Color(1f, 1f, 1f, 0f);
        _handPivot.Position = contact + new Vector2(0f, -58f);
        _handPivot.Rotation = -0.045f;

        var enter = CreateTween().SetParallel(true);
        enter.TweenProperty(_idleCharacter, "modulate:a", 0f, 0.10f);
        enter.TweenProperty(_reactionCharacter, "modulate:a", 1f, 0.12f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        enter.TweenProperty(_reactionGlow, "modulate:a", 0.13f, 0.16f);
        enter.TweenProperty(_hand, "modulate:a", 1f, 0.10f);
        enter.TweenProperty(_crystalLightsRoot, "modulate:a", 0f, 0.10f);
        enter.TweenProperty(_handPivot, "position", contact, 0.14f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await ToSignal(enter, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        _idleGlow.Visible = false;
        _crystalLightsRoot.Hide();
        var bodyBounce = CreateTween();
        bodyBounce.TweenProperty(_reactionMotion, "position", new Vector2(0f, 8f), 0.16f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        bodyBounce.Parallel().TweenProperty(_reactionMotion, "scale", new Vector2(0.992f, 1.008f), 0.16f);
        bodyBounce.TweenProperty(_reactionMotion, "position", Vector2.Zero, 0.28f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        bodyBounce.Parallel().TweenProperty(_reactionMotion, "scale", Vector2.One, 0.28f);

        var pat = CreateTween();
        AddPatStroke(pat, contact + new Vector2(-13f, 5f), -0.070f, 0.15f);
        AddPatStroke(pat, contact + new Vector2(13f, -1f), 0.035f, 0.15f);
        AddPatStroke(pat, contact + new Vector2(-10f, 5f), -0.060f, 0.15f);
        AddPatStroke(pat, contact + new Vector2(8f, 0f), 0.020f, 0.15f);
        await ToSignal(pat, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }
        await ToSignal(GetTree().CreateTimer(0.30), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive)
        {
            return;
        }

        var leave = CreateTween().SetParallel(true);
        leave.TweenProperty(_handPivot, "position", contact + new Vector2(8f, -55f), 0.16f)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        leave.TweenProperty(_hand, "modulate:a", 0f, 0.13f);
        leave.TweenProperty(_reactionCharacter, "modulate:a", 0f, 0.13f);
        leave.TweenProperty(_reactionGlow, "modulate:a", 0f, 0.13f);
        leave.TweenProperty(_idleCharacter, "modulate:a", 1f, 0.15f);
        await ToSignal(leave, Tween.SignalName.Finished);
        if (!_sceneAlive)
        {
            return;
        }

        _hand.Visible = false;
        FinishReactionVisuals();
        _crystalLightsRoot.Modulate = Colors.White;
        _crystalLightsRoot.Show();
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive)
        {
            return;
        }
        FinishInteraction();
    }

    private void AddPatStroke(Tween tween, Vector2 position, float rotation, double duration)
    {
        tween.TweenProperty(_handPivot, "position", position, duration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
        tween.Parallel().TweenProperty(_handPivot, "rotation", rotation, duration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
    }

    private int DrawVariant(List<int> bag, int variantCount, ref int lastVariant)
    {
        if (bag.Count == 0)
        {
            for (var i = 0; i < variantCount; i++)
            {
                bag.Add(i);
            }
            for (var i = bag.Count - 1; i > 0; i--)
            {
                var j = _rng.RandiRange(0, i);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            if (bag.Count > 1 && bag[0] == lastVariant)
            {
                (bag[0], bag[1]) = (bag[1], bag[0]);
            }
        }

        var result = bag[0];
        bag.RemoveAt(0);
        lastVariant = result;
        return result;
    }

    private void SetInteractionEnabled(bool enabled)
    {
        _headPatButton.Disabled = !enabled;
        _highFiveButton.Disabled = !enabled;
        foreach (var button in _crystalButtons)
        {
            button.Disabled = !enabled;
        }
    }

    private void FinishInteraction()
    {
        if (!_sceneAlive)
        {
            return;
        }

        SetInteractionEnabled(true);
        _isReacting = false;
        if (CountLitCrystals() > 0)
        {
            ScheduleCrystalReset();
        }
        else
        {
            _crystalResetVersion++;
        }
    }

    private async void ScheduleCrystalReset()
    {
        var version = ++_crystalResetVersion;
        await ToSignal(GetTree().CreateTimer(10.0), SceneTreeTimer.SignalName.Timeout);
        if (!_sceneAlive || version != _crystalResetVersion || _isReacting || CountLitCrystals() == 0)
        {
            return;
        }

        _isReacting = true;
        SetInteractionEnabled(false);
        for (var i = 0; i < _crystalLit.Length; i++)
        {
            if (!_crystalLit[i])
            {
                continue;
            }

            await ExtinguishCrystal(i);
            if (!_sceneAlive)
            {
                return;
            }
            await ToSignal(GetTree().CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
            if (!_sceneAlive)
            {
                return;
            }
        }

        _warningCount = 0;
        _crystalResetVersion++;
        SetInteractionEnabled(true);
        _isReacting = false;
    }
}
