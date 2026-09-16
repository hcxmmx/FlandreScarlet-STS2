#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace Hcxmmx.FlandreScarletMod.Scripts;

public partial class FlandreController : Node2D
{
    private const float EmphasisChance = 0.25f;
    private const float EmphasisScaleMultiplier = 1.065f;

    private enum AttackSeries
    {
        None,
        Light,
        Sword,
    }

    private sealed record AttackVariant(
        string TexturePath,
        string AnimationName,
        Vector2 Offset,
        float Scale = 0.25f,
        float RotationDegrees = 0f);

    private readonly record struct EmphasisTiming(float Start, float Peak, float HoldEnd, float End);

    private static readonly AttackVariant[] LightAttackVariants =
    [
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Attack/attack_01.png", "Attack", new Vector2(0f, -768f)),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Attack/attack_02.png", "Attack", new Vector2(0f, -768f), 0.248f, -0.5f),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Attack/attack_03.png", "Attack_03", new Vector2(0f, -768f), 0.242f, 0.9f),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Attack/attack_04.png", "Attack_04", new Vector2(0f, -768f), 0.246f, -0.8f),
    ];

    private static readonly AttackVariant[] SwordAttackVariants =
    [
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Sword/sword_01.png", "SwordAttack", new Vector2(0f, -701f), 0.25f),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Sword/sword_02.png", "SwordAttack", new Vector2(0f, -724f), 0.245f, 0.8f),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Sword/sword_03.png", "SwordAttack_03", new Vector2(0f, -768f), 0.242f, 1.1f),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Sword/sword_04.png", "SwordAttack", new Vector2(0f, -724f), 0.25f, -0.4f),
        new("res://Hcxmmx_FlandreScarlet/Assets/Character/Sword/sword_05.png", "SwordAttack_05", new Vector2(0f, -701f), 0.248f, -0.9f),
    ];

    private static readonly StringName[] HeavyAttackVariants =
    [
        "heavyAttack",
        "HeavyAttack_03",
        "HeavyAttack_05",
        "HeavyAttack_06",
        "HeavyAttack_07",
        "HeavyShatter",
        "HeavyDash",
        "HeavyFourfold",
    ];

    private static readonly StringName[] CastVariants =
    [
        "Cast",
        "CastCrystalOrb",
        "CastScarletCollapse",
    ];

    private static readonly StringName[] HitVariants =
    [
        "Hit",
        "HitGuard",
        "HitHat",
        "HitCrouch",
    ];

    private static readonly StringName[] PowerUpVariants =
    [
        "PowerUp",
        "PowerUpRedMoon",
        "PowerUpCrystalResonance",
    ];

    private static readonly StringName[] IntroVariants =
    [
        "Intro",
        "IntroCrystalAwakening",
    ];

    private static readonly StringName[] DeadVariants =
    [
        "Dead",
        "DeadEhehe",
        "DeadHug",
    ];

    private static readonly StringName[] VictoryVariants =
    [
        "Victory",
        "VictoryCheer",
    ];

    private static int _lastIntroVariantIndex = -1;

    private AnimationPlayer? _animationPlayer;
    private Node2D? _animationRoot;
    private Sprite2D? _poseSprite;
    private Sprite2D? _attackGhost;
    private Node2D? _originalBody;
    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<string, Texture2D> _textureCache = new();
    private AttackSeries _lockedAttackSeries;
    private int _lastLightAttackIndex = -1;
    private int _lastSwordAttackIndex = -1;
    private readonly List<int> _heavyAttackBag = new();
    private int _lastHeavyAttackIndex = -1;
    private readonly List<int> _castBag = new();
    private int _lastCastIndex = -1;
    private readonly List<int> _hitBag = new();
    private int _lastHitIndex = -1;
    private readonly List<int> _powerUpBag = new();
    private int _lastPowerUpIndex = -1;
    private EmphasisTiming _emphasisTiming;
    private float _emphasisScale = 1f;
    private bool _emphasisActive;
    private bool _previousAttackWasEmphasized;
    private bool _isDead;
    private bool _isVictorious;
    private bool _victoryPending;
    private StringName _pendingVictoryAnimation = "Victory";
    private bool _introPlayed;
    private bool _introPending;

    public override void _Ready()
    {
        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (_animationPlayer == null)
        {
            GD.PrintErr("[FlandreScarlet] AnimationPlayer not found.");
            return;
        }

        _poseSprite = GetNodeOrNull<Sprite2D>("AnimationRoot/FacingCorrection/PoseSprite");
        _attackGhost = GetNodeOrNull<Sprite2D>("AnimationRoot/FacingCorrection/AttackGhost");
        _animationRoot = GetNodeOrNull<Node2D>("AnimationRoot");
        _random.Randomize();
        _animationPlayer.AnimationFinished += OnAnimationFinished;
        FlandreCombat.ActiveControllers.Add(this);
        PlayIdle();
    }

    public override void _ExitTree()
    {
        FlandreCombat.ActiveControllers.Remove(this);
    }

    public override void _Process(double delta)
    {
        UpdateAttackEmphasis();
        SyncFacingDirection();
    }

    public void Initialize(Node2D? originalBody)
    {
        _originalBody = originalBody;
        _isDead = false;
        _isVictorious = false;
        _victoryPending = false;
        _pendingVictoryAnimation = "Victory";
        _previousAttackWasEmphasized = false;
        _heavyAttackBag.Clear();
        _lastHeavyAttackIndex = -1;
        _castBag.Clear();
        _lastCastIndex = -1;
        _hitBag.Clear();
        _lastHitIndex = -1;
        _powerUpBag.Clear();
        _lastPowerUpIndex = -1;
        ClearAttackSeries();
        SyncFacingDirection();
        if (!_introPlayed)
        {
            // _Ready() 会先建立安全的待机状态，但游戏的房间淡入尚未结束。
            // 等待期间隐藏整个动画根节点，避免黑幕即将退去时漏出一帧待机。
            _animationRoot?.Hide();
            ScheduleIntroAfterRoomFade();
        }
        else
        {
            _animationRoot?.Show();
            PlayIdle();
        }
    }

    public void HandleGameTrigger(string trigger)
    {
        if (_animationPlayer == null || _isDead || _isVictorious)
        {
            return;
        }

        switch (trigger)
        {
            case "Intro":
                ClearAttackSeries();
                PlayAction("Intro");
                break;
            case "IntroCrystalAwakening":
                ClearAttackSeries();
                PlayAction("IntroCrystalAwakening");
                break;
            case "Attack":
            case "AttackSingle":
            case "AttackTriple":
                PlayRandomAttack();
                break;
            case "heavyAttack":
                ClearAttackSeries();
                PlayRandomHeavyAttack();
                break;
            case "HeavyShatter":
                ClearAttackSeries();
                PlayAction("HeavyShatter");
                break;
            case "HeavyDash":
                ClearAttackSeries();
                PlayAction("HeavyDash");
                break;
            case "HeavyFourfold":
                ClearAttackSeries();
                PlayAction("HeavyFourfold");
                break;
            case "HeavyAttack_03":
            case "HeavyAttack_05":
            case "HeavyAttack_06":
            case "HeavyAttack_07":
                ClearAttackSeries();
                PlayAction(trigger);
                break;
            case "Cast":
                ClearAttackSeries();
                PlayRandomCast();
                break;
            case "CastCrystalOrb":
                ClearAttackSeries();
                PlayAction("CastCrystalOrb");
                break;
            case "CastScarletCollapse":
                ClearAttackSeries();
                PlayAction("CastScarletCollapse");
                break;
            case "PowerUp":
                ClearAttackSeries();
                PlayRandomPowerUp();
                break;
            case "PowerUpRedMoon":
                ClearAttackSeries();
                PlayAction("PowerUpRedMoon");
                break;
            case "PowerUpCrystalResonance":
                ClearAttackSeries();
                PlayAction("PowerUpCrystalResonance");
                break;
            case "Hit":
                ClearAttackSeries();
                PlayRandomHit();
                break;
            case "HitGuard":
                ClearAttackSeries();
                PlayAction("HitGuard");
                break;
            case "HitHat":
                ClearAttackSeries();
                PlayAction("HitHat");
                break;
            case "HitCrouch":
                ClearAttackSeries();
                PlayAction("HitCrouch");
                break;
            case "Dead":
            case "Death":
            case "Die":
                PlayDead(DeadVariants[_random.RandiRange(0, DeadVariants.Length - 1)]);
                break;
            case "DeadEhehe":
            case "DeadHug":
                PlayDead(trigger);
                break;
            case "Victory":
                RequestVictory();
                break;
            case "Victory495":
                RequestVictory("Victory");
                break;
            case "VictoryCheer":
                RequestVictory("VictoryCheer");
                break;
            case "Idle":
            case "Relaxed":
                if (!_animationPlayer.IsPlaying() || _animationPlayer.CurrentAnimation == "Idle")
                {
                    PlayIdle();
                }
                break;
        }
    }

    private void PlayDead(StringName animationName)
    {
        if (_animationPlayer == null)
        {
            return;
        }

        ClearAttackSeries();
        _victoryPending = false;
        _isVictorious = false;
        _isDead = true;
        _animationPlayer.ClearQueue();
        ResetVisualState();
        _animationPlayer.Play(animationName);
        _animationPlayer.Advance(0.0);
    }

    internal void RequestVictory(StringName? forcedAnimation = null)
    {
        if (_animationPlayer == null || _isDead || _isVictorious || _victoryPending)
        {
            return;
        }

        _pendingVictoryAnimation = forcedAnimation
            ?? VictoryVariants[_random.RandiRange(0, VictoryVariants.Length - 1)];

        string current = _animationPlayer.CurrentAnimation;
        if (_animationPlayer.IsPlaying() && current != "Idle" && current != "RESET")
        {
            _victoryPending = true;
            return;
        }

        StartVictory();
    }

    private void StartVictory()
    {
        if (_animationPlayer == null || _isDead)
        {
            return;
        }

        _victoryPending = false;
        _isVictorious = true;
        ClearAttackSeries();
        _animationPlayer.ClearQueue();
        ResetVisualState();
        _animationPlayer.Play(_pendingVictoryAnimation);
        _animationPlayer.Advance(0.0);
    }

    public int GetDebugAttackVariantCount(bool swordSeries)
    {
        return swordSeries ? SwordAttackVariants.Length : LightAttackVariants.Length;
    }

    public void DebugPlayAttackVariant(bool swordSeries, int variantIndex, int emphasisMode)
    {
        _isDead = false;
        AttackVariant[] variants = swordSeries ? SwordAttackVariants : LightAttackVariants;
        int index = Math.Clamp(variantIndex, 0, variants.Length - 1);
        AttackVariant variant = variants[index];

        bool emphasized = emphasisMode switch
        {
            1 => true,
            2 => !_previousAttackWasEmphasized && _random.Randf() < EmphasisChance,
            _ => false,
        };

        if (emphasisMode == 2)
        {
            _previousAttackWasEmphasized = emphasized;
        }

        PlayAction(variant.AnimationName, () => ApplyAttackVariant(variant));
        StartAttackEmphasis(variant.AnimationName, emphasized);
    }

    public void DebugPlayRandomAttack()
    {
        _isDead = false;
        _isVictorious = false;
        _victoryPending = false;
        _pendingVictoryAnimation = "Victory";
        PlayRandomAttack();
    }

    public void DebugPlayTrigger(string trigger)
    {
        if (trigger is not ("Dead" or "Death" or "Die" or "DeadEhehe" or "DeadHug"))
        {
            _isDead = false;
        }

        if (trigger != "Victory")
        {
            _isVictorious = false;
            _victoryPending = false;
        }

        HandleGameTrigger(trigger);
    }

    public void DebugReset()
    {
        _isDead = false;
        _isVictorious = false;
        _victoryPending = false;
        _previousAttackWasEmphasized = false;
        ClearAttackSeries();
        PlayIdle();
    }

    private void PlayRandomAttack()
    {
        if (_animationPlayer == null)
        {
            return;
        }

        bool continuesCurrentCombo =
            _animationPlayer.IsPlaying() && IsRandomAttackAnimation(_animationPlayer.CurrentAnimation);

        if (!continuesCurrentCombo || _lockedAttackSeries == AttackSeries.None)
        {
            _lockedAttackSeries = _random.RandiRange(0, 1) == 0
                ? AttackSeries.Light
                : AttackSeries.Sword;
        }

        AttackVariant variant;
        StringName animationName;
        if (_lockedAttackSeries == AttackSeries.Sword)
        {
            int index = PickNonRepeatingIndex(SwordAttackVariants.Length, _lastSwordAttackIndex);
            _lastSwordAttackIndex = index;
            variant = SwordAttackVariants[index];
        }
        else
        {
            int index = PickNonRepeatingIndex(LightAttackVariants.Length, _lastLightAttackIndex);
            _lastLightAttackIndex = index;
            variant = LightAttackVariants[index];
        }

        animationName = variant.AnimationName;

        bool emphasized = !_previousAttackWasEmphasized && _random.Randf() < EmphasisChance;
        _previousAttackWasEmphasized = emphasized;

        PlayAction(animationName, () => ApplyAttackVariant(variant));
        StartAttackEmphasis(animationName, emphasized);
    }

    private int PickNonRepeatingIndex(int count, int previousIndex)
    {
        if (count <= 1)
        {
            return 0;
        }

        if (previousIndex < 0 || previousIndex >= count)
        {
            return _random.RandiRange(0, count - 1);
        }

        int index = _random.RandiRange(0, count - 2);
        return index >= previousIndex ? index + 1 : index;
    }

    private void PlayRandomHeavyAttack()
    {
        if (_heavyAttackBag.Count == 0)
        {
            RefillHeavyAttackBag();
        }

        int index = _heavyAttackBag[^1];
        _heavyAttackBag.RemoveAt(_heavyAttackBag.Count - 1);
        _lastHeavyAttackIndex = index;
        PlayAction(HeavyAttackVariants[index]);
    }

    private void RefillHeavyAttackBag()
    {
        _heavyAttackBag.Clear();
        for (int i = 0; i < HeavyAttackVariants.Length; i++)
        {
            _heavyAttackBag.Add(i);
        }

        for (int i = _heavyAttackBag.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.RandiRange(0, i);
            (_heavyAttackBag[i], _heavyAttackBag[swapIndex]) =
                (_heavyAttackBag[swapIndex], _heavyAttackBag[i]);
        }

        if (_heavyAttackBag.Count > 1 && _heavyAttackBag[^1] == _lastHeavyAttackIndex)
        {
            (_heavyAttackBag[^1], _heavyAttackBag[0]) =
                (_heavyAttackBag[0], _heavyAttackBag[^1]);
        }
    }

    private void PlayRandomCast()
    {
        if (_castBag.Count == 0)
        {
            RefillCastBag();
        }

        int index = _castBag[^1];
        _castBag.RemoveAt(_castBag.Count - 1);
        _lastCastIndex = index;
        PlayAction(CastVariants[index]);
    }

    private void RefillCastBag()
    {
        _castBag.Clear();
        for (int i = 0; i < CastVariants.Length; i++)
        {
            _castBag.Add(i);
        }

        for (int i = _castBag.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.RandiRange(0, i);
            (_castBag[i], _castBag[swapIndex]) =
                (_castBag[swapIndex], _castBag[i]);
        }

        // 从列表尾部抽取；若新一轮第一张与上一轮末张相同，就交换掉。
        if (_castBag.Count > 1 && _castBag[^1] == _lastCastIndex)
        {
            (_castBag[^1], _castBag[0]) =
                (_castBag[0], _castBag[^1]);
        }
    }

    private void PlayRandomHit()
    {
        if (_hitBag.Count == 0)
        {
            RefillHitBag();
        }

        int index = _hitBag[^1];
        _hitBag.RemoveAt(_hitBag.Count - 1);
        _lastHitIndex = index;
        PlayAction(HitVariants[index]);
    }

    private void RefillHitBag()
    {
        _hitBag.Clear();
        for (int i = 0; i < HitVariants.Length; i++)
        {
            _hitBag.Add(i);
        }

        for (int i = _hitBag.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.RandiRange(0, i);
            (_hitBag[i], _hitBag[swapIndex]) =
                (_hitBag[swapIndex], _hitBag[i]);
        }

        if (_hitBag.Count > 1 && _hitBag[^1] == _lastHitIndex)
        {
            (_hitBag[^1], _hitBag[0]) =
                (_hitBag[0], _hitBag[^1]);
        }
    }

    private void PlayRandomPowerUp()
    {
        if (_powerUpBag.Count == 0)
        {
            RefillPowerUpBag();
        }

        int index = _powerUpBag[^1];
        _powerUpBag.RemoveAt(_powerUpBag.Count - 1);
        _lastPowerUpIndex = index;
        PlayAction(PowerUpVariants[index]);
    }

    private void RefillPowerUpBag()
    {
        _powerUpBag.Clear();
        for (int i = 0; i < PowerUpVariants.Length; i++)
        {
            _powerUpBag.Add(i);
        }

        for (int i = _powerUpBag.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.RandiRange(0, i);
            (_powerUpBag[i], _powerUpBag[swapIndex]) =
                (_powerUpBag[swapIndex], _powerUpBag[i]);
        }

        if (_powerUpBag.Count > 1 && _powerUpBag[^1] == _lastPowerUpIndex)
        {
            (_powerUpBag[^1], _powerUpBag[0]) =
                (_powerUpBag[0], _powerUpBag[^1]);
        }
    }

    private void ApplyAttackVariant(AttackVariant variant)
    {
        Texture2D? texture = LoadAttackTexture(variant.TexturePath);
        if (texture == null)
        {
            return;
        }

        Vector2 scale = Vector2.One * variant.Scale;
        float rotation = Mathf.DegToRad(variant.RotationDegrees);
        foreach (Sprite2D? sprite in new[] { _poseSprite, _attackGhost })
        {
            if (sprite == null)
            {
                continue;
            }

            sprite.Texture = texture;
            sprite.Offset = variant.Offset;
            sprite.Scale = scale;
            sprite.Rotation = rotation;
        }
    }

    private Texture2D? LoadAttackTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        if (texture == null)
        {
            GD.PrintErr($"[FlandreScarlet] Attack texture not found: {path}");
            return null;
        }

        _textureCache[path] = texture;
        return texture;
    }

    private void StartAttackEmphasis(StringName animationName, bool emphasized)
    {
        StopAttackEmphasis();
        if (!emphasized)
        {
            return;
        }

        _emphasisTiming = GetEmphasisTiming(animationName.ToString());
        _emphasisActive = true;
    }

    private void UpdateAttackEmphasis()
    {
        if (!_emphasisActive || _animationPlayer == null
            || !_animationPlayer.IsPlaying()
            || !IsRandomAttackAnimation(_animationPlayer.CurrentAnimation))
        {
            return;
        }

        float time = (float)_animationPlayer.CurrentAnimationPosition;
        if (time < _emphasisTiming.Start)
        {
            _emphasisScale = 1f;
        }
        else if (time < _emphasisTiming.Peak)
        {
            float progress = Mathf.InverseLerp(_emphasisTiming.Start, _emphasisTiming.Peak, time);
            _emphasisScale = Mathf.Lerp(1f, EmphasisScaleMultiplier, progress * progress * progress);
        }
        else if (time < _emphasisTiming.HoldEnd)
        {
            _emphasisScale = EmphasisScaleMultiplier;
        }
        else if (time < _emphasisTiming.End)
        {
            float progress = Mathf.InverseLerp(_emphasisTiming.HoldEnd, _emphasisTiming.End, time);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            _emphasisScale = Mathf.Lerp(EmphasisScaleMultiplier, 1f, easedProgress);
        }
        else
        {
            StopAttackEmphasis();
        }
    }

    private static EmphasisTiming GetEmphasisTiming(string animationName)
    {
        return animationName switch
        {
            "Attack_03" => new EmphasisTiming(0.14f, 0.28f, 0.46f, 0.64f),
            "Attack_04" => new EmphasisTiming(0.14f, 0.24f, 0.42f, 0.64f),
            "SwordAttack_03" => new EmphasisTiming(0.17f, 0.34f, 0.52f, 0.72f),
            "SwordAttack_05" => new EmphasisTiming(0.16f, 0.27f, 0.45f, 0.7f),
            "SwordAttack" => new EmphasisTiming(0.09f, 0.16f, 0.34f, 0.62f),
            _ => new EmphasisTiming(0.08f, 0.14f, 0.3f, 0.52f),
        };
    }

    private void StopAttackEmphasis()
    {
        _emphasisActive = false;
        _emphasisScale = 1f;
    }

    private void PlayAction(StringName animationName, Action? prepareVisuals = null)
    {
        if (_animationPlayer == null || !_animationPlayer.HasAnimation(animationName))
        {
            GD.PrintErr($"[FlandreScarlet] Animation not found: {animationName}");
            return;
        }

        _animationPlayer.ClearQueue();
        ResetVisualState();
        _animationPlayer.Play(animationName);
        // Play() 只安排播放，属性通常要到下一处理帧才会生效。
        // 立即应用目标动画的第 0 帧，避免 RESET 的待机姿势闪现一帧。
        _animationPlayer.Advance(0.0);
        prepareVisuals?.Invoke();
    }

    private void PlayIdle()
    {
        if (_animationPlayer == null || _isDead || _isVictorious || !_animationPlayer.HasAnimation("Idle"))
        {
            return;
        }

        ResetVisualState();
        _animationPlayer.Play("Idle");
        _animationPlayer.Advance(0.0);
    }

    private async void ScheduleIntroAfterRoomFade()
    {
        if (_introPending || _introPlayed)
        {
            return;
        }

        _introPending = true;
        SceneTree? tree = GetTree();
        if (tree == null)
        {
            _introPending = false;
            _animationRoot?.Show();
            return;
        }

        // Initialize() 通常发生在房间淡入开始前后。先让出一帧，确保游戏的
        // 转场节点已更新，再等待黑色遮罩基本退去，避免入场动画在黑屏后播放一半。
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        while (IsInsideTree() && IsRoomTransitionCoveringScreen())
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        if (!IsInsideTree() || _isDead)
        {
            _introPending = false;
            _animationRoot?.Show();
            return;
        }

        await ToSignal(tree.CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || _isDead)
        {
            _introPending = false;
            _animationRoot?.Show();
            return;
        }

        _introPending = false;
        _introPlayed = true;
        // 先在隐藏状态下应用 Intro 的第 0 帧（人物透明、血月尚未出现），
        // 再显示根节点，确保不会渲染到 RESET 的待机画面。
        int introIndex = PickNonRepeatingIndex(IntroVariants.Length, _lastIntroVariantIndex);
        _lastIntroVariantIndex = introIndex;
        PlayAction(IntroVariants[introIndex]);
        _animationRoot?.Show();
    }

    private static bool IsRoomTransitionCoveringScreen()
    {
        var transition = NGame.Instance?.Transition;
        if (transition == null)
        {
            return false;
        }

        CanvasItem? simpleTransition = transition.GetNodeOrNull<CanvasItem>("SimpleTransition");
        return transition.InTransition
            || (simpleTransition != null && simpleTransition.Modulate.A > 0.12f);
    }

    private void ResetVisualState()
    {
        StopAttackEmphasis();

        if (_animationPlayer == null || !_animationPlayer.HasAnimation("RESET"))
        {
            return;
        }

        _animationPlayer.Play("RESET");
        _animationPlayer.Advance(0.0);
    }

    private void OnAnimationFinished(StringName animationName)
    {
        if (IsRandomAttackAnimation(animationName))
        {
            ClearAttackSeries();
        }

        if (_victoryPending && !_isDead)
        {
            StartVictory();
            return;
        }

        if (!_isDead && !_isVictorious && animationName != "Idle" && animationName != "RESET")
        {
            PlayIdle();
        }
    }

    private static bool IsRandomAttackAnimation(StringName animationName)
    {
        string name = animationName.ToString();
        return name == "Attack" || name.StartsWith("Attack_", StringComparison.Ordinal)
            || name == "SwordAttack" || name.StartsWith("SwordAttack_", StringComparison.Ordinal);
    }

    private void ClearAttackSeries()
    {
        _lockedAttackSeries = AttackSeries.None;
    }

    private void SyncFacingDirection()
    {
        float direction = 1f;
        if (_originalBody != null && GodotObject.IsInstanceValid(_originalBody))
        {
            direction = Mathf.Sign(_originalBody.Scale.X);
        }

        if (direction == 0f)
        {
            return;
        }

        Scale = new Vector2(_emphasisScale * direction, _emphasisScale);
    }
}
