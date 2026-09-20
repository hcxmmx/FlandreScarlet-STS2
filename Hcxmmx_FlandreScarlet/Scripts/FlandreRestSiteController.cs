#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace Hcxmmx.FlandreScarletMod.Scripts;

public partial class FlandreRestSiteController : Node2D
{
    private static readonly string[] Lines =
    {
        "火焰看起来很好玩……可以碰一下吗？",
        "姐姐平时可不会让我跑这么远。",
        "这里什么都可以弄坏吗？",
        "这座塔里，还藏着多少有趣的东西呢？",
        "咲夜做的点心比这个好多了。",
        "休息结束后，就继续往上走吧？",
        "外面的景色，和红魔馆里完全不一样呢。",
        "这团火……捏碎以后会变成什么？",
        "你不会把我一个人留在这里吧？",
        "一直往上走的话，最后会看到什么呢？",
        "今天遇到的家伙，都挺有趣的。",
        "稍微休息一下也没关系……只有一下哦。",
        "姐姐知道我跑出来，会是什么表情呢？",
        "这里很安静，不过还没有地下室安静。"
    };

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<int> _lineBag = new();
    private Godot.Timer? _speechTimer;
    private NRestSiteCharacter? _character;
    private NRestSiteRoom? _room;
    private NSpeechBubbleVfx? _activeBubble;
    private bool _singlePlayer;

    public override void _Ready()
    {
        _character = GetParentOrNull<NRestSiteCharacter>();
        if (_character == null)
        {
            return;
        }

        _singlePlayer = _character.Player.RunState.Players.Count == 1;
        if (!_singlePlayer)
        {
            return;
        }

        _room = FindRestSiteRoom(_character);
        if (_room == null)
        {
            GD.PrintErr("[FlandreScarlet] Rest-site room was not found for dialogue bubbles.");
            return;
        }

        _rng.Randomize();
        RefillLineBag();

        _speechTimer = new Godot.Timer
        {
            OneShot = true,
            ProcessCallback = Godot.Timer.TimerProcessCallback.Idle
        };
        AddChild(_speechTimer);
        _speechTimer.Timeout += OnSpeechTimerTimeout;
        ScheduleNextLine(firstLine: true);
    }

    public override void _ExitTree()
    {
        if (_speechTimer != null)
        {
            _speechTimer.Timeout -= OnSpeechTimerTimeout;
        }

        if (_activeBubble != null && GodotObject.IsInstanceValid(_activeBubble))
        {
            _activeBubble.QueueFree();
        }

        _activeBubble = null;
    }

    private void OnSpeechTimerTimeout()
    {
        if (!_singlePlayer || _character == null || _room == null || !IsInsideTree())
        {
            return;
        }

        if (_lineBag.Count == 0)
        {
            RefillLineBag();
        }

        var lineIndex = _lineBag[^1];
        _lineBag.RemoveAt(_lineBag.Count - 1);

        var speechPosition = _character.ToGlobal(new Vector2(115f, -370f));
        var bubble = NSpeechBubbleVfx.Create(
            Lines[lineIndex],
            DialogueSide.Left,
            speechPosition,
            3.2,
            VfxColor.Red);

        if (bubble != null)
        {
            bubble.Scale = Vector2.One * 0.67f;
            _activeBubble = bubble;
            bubble.TreeExited += () =>
            {
                if (_activeBubble == bubble)
                {
                    _activeBubble = null;
                }
            };
            _room.AddChild(bubble);
        }

        ScheduleNextLine(firstLine: false);
    }

    private void ScheduleNextLine(bool firstLine)
    {
        if (_speechTimer == null)
        {
            return;
        }

        _speechTimer.WaitTime = firstLine
            ? _rng.RandfRange(3f, 5f)
            : _rng.RandfRange(10f, 18f);
        _speechTimer.Start();
    }

    private void RefillLineBag()
    {
        _lineBag.Clear();
        for (var i = 0; i < Lines.Length; i++)
        {
            _lineBag.Add(i);
        }

        for (var i = _lineBag.Count - 1; i > 0; i--)
        {
            var swapIndex = _rng.RandiRange(0, i);
            (_lineBag[i], _lineBag[swapIndex]) = (_lineBag[swapIndex], _lineBag[i]);
        }
    }

    private static NRestSiteRoom? FindRestSiteRoom(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is NRestSiteRoom room)
            {
                return room;
            }

            current = current.GetParent();
        }

        return null;
    }
}
