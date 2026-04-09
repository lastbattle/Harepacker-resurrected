using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace UnitTest_MapSimulator;

public sealed class EquipmentWindowParityTests
{
    [Fact]
    public void ShouldBlockDragStart_UsesClientShaped500MsBoundary()
    {
        const int requestTick = 1_000;

        Assert.True(EquipmentChangeClientParity.ShouldBlockDragStart(requestTick + 499, requestTick, hasPendingRequest: false));
        Assert.False(EquipmentChangeClientParity.ShouldBlockDragStart(requestTick + 500, requestTick, hasPendingRequest: false));
        Assert.True(EquipmentChangeClientParity.ShouldBlockDragStart(requestTick + 900, requestTick, hasPendingRequest: true));
    }

    [Fact]
    public void InventorySourceRejectReason_AllowsSelfOwnedPendingLock()
    {
        var request = new EquipmentChangeRequest
        {
            RequestId = 41,
            ItemId = 1302000
        };
        var liveSlot = new InventorySlotData
        {
            ItemId = 1302000,
            IsDisabled = true,
            PendingRequestId = 41
        };

        bool rejected = EquipmentChangeRequestValidator.TryGetInventorySourceRejectReason(request, liveSlot, out string rejectReason);

        Assert.False(rejected);
        Assert.Equal(string.Empty, rejectReason);
    }

    [Fact]
    public void Unequip_RemovesCashItemAndRestoresHiddenBaseEquip()
    {
        CharacterBuild build = CreateBuildWithCashWeaponOverlay();

        CharacterPart removed = build.Unequip(EquipSlot.Weapon);

        Assert.NotNull(removed);
        Assert.True(removed.IsCash);
        Assert.Equal(1703000, removed.ItemId);
        Assert.True(build.Equipment.TryGetValue(EquipSlot.Weapon, out CharacterPart restored));
        Assert.Equal(1302000, restored.ItemId);
        Assert.False(build.HiddenEquipment.ContainsKey(EquipSlot.Weapon));
    }

    [Fact]
    public void PlaceEquipment_BaseEquipUnderCash_DisplacesOnlyPreviousHiddenBase()
    {
        CharacterBuild build = CreateBuildWithCashWeaponOverlay();
        var replacementBase = new WeaponPart
        {
            ItemId = 1312000,
            Name = "Replacement Axe",
            Slot = EquipSlot.Weapon,
            IsTwoHanded = false
        };

        IReadOnlyList<CharacterPart> displaced = build.PlaceEquipment(replacementBase, EquipSlot.Weapon);

        Assert.Single(displaced);
        Assert.Equal(1302000, displaced[0].ItemId);
        Assert.True(build.Equipment.TryGetValue(EquipSlot.Weapon, out CharacterPart visible));
        Assert.Equal(1703000, visible.ItemId);
        Assert.True(build.HiddenEquipment.TryGetValue(EquipSlot.Weapon, out CharacterPart concealed));
        Assert.Equal(1312000, concealed.ItemId);
    }

    [Fact]
    public void ShieldVisualState_UsesHiddenTwoHandedWeaponUnderCashCover()
    {
        CharacterBuild build = CreateBuildWithCashWeaponOverlay(twoHandedBaseWeapon: true);

        EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Shield);

        Assert.True(state.IsDisabled);
        Assert.Equal(EquipSlotDisableReason.TwoHandedWeapon, state.Reason);
    }

    [Fact]
    public void Clone_DeepClonesVisibleAndHiddenEquipmentLayers()
    {
        CharacterBuild original = CreateBuildWithCashWeaponOverlay();

        CharacterBuild clone = original.Clone();
        CharacterPart cloneVisible = clone.Equipment[EquipSlot.Weapon];
        CharacterPart cloneHidden = clone.HiddenEquipment[EquipSlot.Weapon];

        Assert.NotSame(original.Equipment[EquipSlot.Weapon], cloneVisible);
        Assert.NotSame(original.HiddenEquipment[EquipSlot.Weapon], cloneHidden);

        cloneVisible.Name = "Clone Cash Cover";
        cloneHidden.Name = "Clone Hidden Weapon";
        cloneHidden.Slot = EquipSlot.Shield;

        Assert.Equal("Cash Cover", original.Equipment[EquipSlot.Weapon].Name);
        Assert.Equal("Base Weapon", original.HiddenEquipment[EquipSlot.Weapon].Name);
        Assert.Equal(EquipSlot.Weapon, original.HiddenEquipment[EquipSlot.Weapon].Slot);
    }

    [Fact]
    public void LoginAvatarLookCodec_RoundTripsVisibleAndHiddenEquipmentSplit()
    {
        CharacterBuild build = CreateBuildWithCashWeaponOverlay();

        LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(build);
        byte[] encoded = LoginAvatarLookCodec.Encode(look);

        bool decoded = LoginAvatarLookCodec.TryDecode(encoded, out LoginAvatarLook roundTripped, out string error);

        Assert.True(decoded, error);
        Assert.NotNull(roundTripped);
        Assert.Contains(roundTripped.VisibleEquipmentByBodyPart, entry => entry.Value == 1703000);
        Assert.Contains(roundTripped.HiddenEquipmentByBodyPart, entry => entry.Value == 1302000);
    }

    [Fact]
    public void ResolutionRejectReason_RejectsSessionMismatch()
    {
        var request = new EquipmentChangeRequest
        {
            OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
            OwnerSessionId = 12,
            RequestedAtTick = 345
        };
        var query = new EquipmentChangeResolutionQuery
        {
            RequestId = 7,
            OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
            OwnerSessionId = 13,
            RequestedAtTick = 345
        };

        bool rejected = EquipmentChangeRequestValidator.TryGetResolutionRejectReason(request, query, out string rejectReason);

        Assert.True(rejected);
        Assert.Equal("The equipment request session is no longer active.", rejectReason);
    }

    [Fact]
    public void ProcessPendingEquipmentChange_UsesSharedPendingWindowSeam()
    {
        var window = new TestPendingEquipmentWindow(hasPendingEquipmentChange: true);

        UIWindowManager.ProcessPendingEquipmentChange(window, inventoryWindow: null);

        Assert.Equal(1, window.ProcessCount);
    }

    [Fact]
    public void ProcessPendingEquipmentChange_SkipsWindowsWithoutPendingWork()
    {
        var window = new TestPendingEquipmentWindow(hasPendingEquipmentChange: false);

        UIWindowManager.ProcessPendingEquipmentChange(window, inventoryWindow: null);

        Assert.Equal(0, window.ProcessCount);
    }

    private static CharacterBuild CreateBuildWithCashWeaponOverlay(bool twoHandedBaseWeapon = false)
    {
        var build = new CharacterBuild();
        var baseWeapon = new WeaponPart
        {
            ItemId = 1302000,
            Name = "Base Weapon",
            Slot = EquipSlot.Weapon,
            IsTwoHanded = twoHandedBaseWeapon
        };
        var cashCover = new WeaponPart
        {
            ItemId = 1703000,
            Name = "Cash Cover",
            Slot = EquipSlot.Weapon,
            IsCash = true,
            IsTwoHanded = false
        };

        build.PlaceEquipment(baseWeapon, EquipSlot.Weapon);
        build.PlaceEquipment(cashCover, EquipSlot.Weapon);
        return build;
    }

    private sealed class TestPendingEquipmentWindow : UIWindowBase, IEquipmentPendingChangeWindow
    {
        private readonly bool _hasPendingEquipmentChange;

        public TestPendingEquipmentWindow(bool hasPendingEquipmentChange)
            : base((HaSharedLibrary.Render.DX.IDXObject)null)
        {
            _hasPendingEquipmentChange = hasPendingEquipmentChange;
        }

        public override string WindowName => "TestPendingEquipmentWindow";

        public int ProcessCount { get; private set; }

        public bool HasPendingEquipmentChange => _hasPendingEquipmentChange;

        public void ProcessPendingEquipmentChange(InventoryUI inventoryWindow)
        {
            ProcessCount++;
        }
    }
}
