using System.Linq;
using Content.Client.Materials;
using Content.Client.Materials.UI;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Shared.Construction.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Materials;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Construction.UI;

[GenerateTypedNameReferences]
public sealed partial class FlatpackCreatorMenu : FancyWindow
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly FlatpackSystem _flatpack;
    private readonly MaterialStorageSystem _materialStorage;

    private EntityUid _owner;

    [ValidatePrototypeId<EntityPrototype>]
    public const string NoBoardEffectId = "FlatpackerNoBoardEffect";

    private EntityUid? _currentBoard = EntityUid.Invalid;

    public event Action? PackButtonPressed;

    public FlatpackCreatorMenu()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _itemSlots = _entityManager.System<ItemSlotsSystem>();
        _flatpack = _entityManager.System<FlatpackSystem>();
        _materialStorage = _entityManager.System<MaterialStorageSystem>();

        PackButton.OnPressed += _ => PackButtonPressed?.Invoke();

        InsertLabel.SetMarkup(Loc.GetString("flatpacker-ui-insert-board"));
    }

    public void SetEntity(EntityUid uid)
    {
        _owner = uid;
        MaterialStorageControl.SetOwner(uid);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entityManager.TryGetComponent<FlatpackCreatorComponent>(_owner, out var flatpacker) ||
            !_itemSlots.TryGetSlot(_owner, flatpacker.SlotId, out var itemSlot))
            return;

        if (flatpacker.Packing)
        {
            PackButton.Disabled = true;
        }
        else if (_currentBoard != null)
        {
            Dictionary<string, int> cost;
            if (_entityManager.TryGetComponent<MachineBoardComponent>(_currentBoard, out var machineBoardComp))
                cost = _flatpack.GetFlatpackCreationCost((_owner, flatpacker), (_currentBoard.Value, machineBoardComp));
            else
                cost = _flatpack.GetFlatpackCreationCost((_owner, flatpacker), null);

            PackButton.Disabled = !_materialStorage.CanChangeMaterialAmount(_owner, cost);
        }

        if (_currentBoard == itemSlot.Item)
            return;

        _currentBoard = itemSlot.Item;
        CostHeaderLabel.Visible = _currentBoard != null;
        InsertLabel.Visible = _currentBoard == null;

        if (_currentBoard is not null)
        {
            string? prototype = null;
            Dictionary<string, int>? cost = null;

            if (_entityManager.TryGetComponent<MachineBoardComponent>(_currentBoard, out var newMachineBoardComp))
            {
                prototype = newMachineBoardComp.Prototype;
                cost = _flatpack.GetFlatpackCreationCost((_owner, flatpacker), (_currentBoard.Value, newMachineBoardComp));
            }
            else if (_entityManager.TryGetComponent<ComputerBoardComponent>(_currentBoard, out var computerBoard))
            {
                prototype = computerBoard.Prototype;
                cost = _flatpack.GetFlatpackCreationCost((_owner, flatpacker), null);
            }

            if (prototype is not null && cost is not null)
            {
                var proto = _prototypeManager.Index<EntityPrototype>(prototype);
                MachineSprite.SetPrototype(prototype);
                MachineNameLabel.SetMessage(proto.Name);
                CostLabel.SetMarkup(GetCostString(cost));
            }
        }
        else
        {
            MachineSprite.SetPrototype(NoBoardEffectId);
            CostLabel.SetMessage(Loc.GetString("flatpacker-ui-no-board-label"));
            MachineNameLabel.SetMessage(" ");
            PackButton.Disabled = true;
        }
    }

    private string GetCostString(Dictionary<string, int> costs)
    {
        var orderedCosts = costs.OrderBy(p => p.Value).ToArray();
        var msg = new FormattedMessage();
        for (var i = 0; i < orderedCosts.Length; i++)
        {
            var (mat, amount) = orderedCosts[i];

            var matProto = _prototypeManager.Index<MaterialPrototype>(mat);

            var sheetVolume = _materialStorage.GetSheetVolume(matProto);
            var sheets = (float) -amount / sheetVolume;
            var amountText = Loc.GetString("lathe-menu-material-amount",
                ("amount", sheets),
                ("unit", Loc.GetString(matProto.Unit)));
            var text = Loc.GetString("lathe-menu-tooltip-display",
                ("amount", amountText),
                ("material", Loc.GetString(matProto.Name)));

            msg.TryAddMarkup(text, out _);

            if (i != orderedCosts.Length - 1)
                msg.PushNewline();
        }

        return msg.ToMarkup();
    }
}
