#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models;

namespace Hcxmmx.FlandreScarletMod.Scripts;

/// <summary>
/// Visual controller used only in merchant rooms.  It intentionally stays
/// separate from the combat controller so event-to-combat transitions cannot
/// carry shop-only animation state into battle.
/// </summary>
public partial class FlandreMerchantController : Node2D
{
    private readonly RandomNumberGenerator _rng = new();
    private MerchantInventory? _inventory;
    private Sprite2D? _idleBody;
    private Sprite2D? _inspectBody;
    private Node2D? _inspectRoot;

    public override void _Ready()
    {
        _idleBody = GetNode<Sprite2D>("VisualRoot/IdleBody");
        _inspectBody = GetNode<Sprite2D>("VisualRoot/InspectBody");
        _inspectRoot = GetNode<Node2D>("VisualRoot/InspectBody/InspectRoot");
        _rng.Randomize();

        ApplyIdleMode();
        if (_inventory != null)
        {
            ChooseRoomMode();
        }
    }

    public void Initialize(MerchantInventory? inventory)
    {
        _inventory = inventory;
        if (IsNodeReady())
        {
            ChooseRoomMode();
        }
    }

    /// <summary>
    /// Resolves the current stock at the instant an inspection animation starts.
    /// Purchased entries have a null model, while Courier-style restocks replace
    /// that model, so this stays correct without caching the initial three relics.
    /// </summary>
    public IReadOnlyList<RelicModel> GetStockedRelics()
    {
        if (_inventory == null)
        {
            return System.Array.Empty<RelicModel>();
        }

        return _inventory.RelicEntries
            .Where(entry => entry.Model != null)
            .Select(entry => entry.Model!)
            .ToArray();
    }

    private void ChooseRoomMode()
    {
        var relics = GetStockedRelics();
        if (relics.Count == 0 || _rng.RandiRange(0, 1) == 0)
        {
            ApplyIdleMode();
            return;
        }

        var relic = relics[_rng.RandiRange(0, relics.Count - 1)];
        ApplyRelicMode(relic);
    }

    private void ApplyIdleMode()
    {
        if (_idleBody != null)
        {
            _idleBody.Visible = true;
        }

        if (_inspectBody != null)
        {
            _inspectBody.Visible = false;
        }

        if (_inspectRoot != null)
        {
            _inspectRoot.Visible = false;
        }
    }

    private void ApplyRelicMode(RelicModel relic)
    {
        if (_idleBody == null || _inspectBody == null || _inspectRoot == null)
        {
            return;
        }

        _idleBody.Visible = false;
        _inspectBody.Visible = true;
        _inspectRoot.Visible = true;
        _inspectRoot.GetNode<Sprite2D>("RelicIcon").Texture = relic.BigIcon;

        GD.Print($"[FlandreScarlet] Merchant inspection selected relic: {relic.Id.Entry}");
    }
}
