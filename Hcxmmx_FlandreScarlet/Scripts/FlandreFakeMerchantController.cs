#nullable enable
using Godot;

namespace Hcxmmx.FlandreScarletMod.Scripts;

/// <summary>
/// Event-room-only visuals for the fake merchant. The displayed pose is chosen
/// once when the room is entered so opening the shop cannot disturb it.
/// </summary>
public partial class FlandreFakeMerchantController : Node2D
{
    private bool _hasFoulPotion;
    private Sprite2D? _idleBody;
    private Sprite2D? _foulPotionBody;

    public override void _Ready()
    {
        _idleBody = GetNode<Sprite2D>("VisualRoot/IdleBody");
        _foulPotionBody = GetNode<Sprite2D>("VisualRoot/FoulPotionBody");
        ApplyPose();
    }

    public void Initialize(bool hasFoulPotion)
    {
        _hasFoulPotion = hasFoulPotion;
        if (IsNodeReady())
        {
            ApplyPose();
        }
    }

    private void ApplyPose()
    {
        if (_idleBody == null || _foulPotionBody == null)
        {
            return;
        }

        _idleBody.Visible = !_hasFoulPotion;
        _foulPotionBody.Visible = _hasFoulPotion;
        GD.Print($"[FlandreScarlet] Fake-merchant pose: {(_hasFoulPotion ? "foul potion" : "watchful")}");
    }
}
