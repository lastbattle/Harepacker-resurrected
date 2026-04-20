using System;
using System.Collections.Generic;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Input;

namespace UnitTest_MapSimulator;

public sealed class SkillMacroOwnerKeyHandlerParityTests
{
    [Fact]
    public void TryGetClientForwardedFunctionKeyIndex_MapsClientFKeyRange()
    {
        Assert.True(SkillMacroOwnerKeyHandler.TryGetClientForwardedFunctionKeyIndex(Keys.F1, out int f1Index));
        Assert.Equal(0, f1Index);

        Assert.True(SkillMacroOwnerKeyHandler.TryGetClientForwardedFunctionKeyIndex(Keys.F12, out int f12Index));
        Assert.Equal(11, f12Index);

        Assert.False(SkillMacroOwnerKeyHandler.TryGetClientForwardedFunctionKeyIndex(Keys.A, out _));
    }

    [Fact]
    public void ShouldApplyCaretBoundaryNavigation_StopsCtrlHomeEndAtParentPath()
    {
        Assert.True(SkillMacroOwnerKeyHandler.ShouldApplyCaretBoundaryNavigation(controlHeld: false));
        Assert.False(SkillMacroOwnerKeyHandler.ShouldApplyCaretBoundaryNavigation(controlHeld: true));
    }

    [Fact]
    public void IsClientForwardedNonFunctionHotkeyPhysicalKey_MatchesDigitLaneOnly()
    {
        Assert.True(SkillMacroOwnerKeyHandler.IsClientForwardedNonFunctionHotkeyPhysicalKey(Keys.D1));
        Assert.True(SkillMacroOwnerKeyHandler.IsClientForwardedNonFunctionHotkeyPhysicalKey(Keys.D8));
        Assert.False(SkillMacroOwnerKeyHandler.IsClientForwardedNonFunctionHotkeyPhysicalKey(Keys.NumPad1));
        Assert.False(SkillMacroOwnerKeyHandler.IsClientForwardedNonFunctionHotkeyPhysicalKey(Keys.Insert));
    }

    [Fact]
    public void TryResolveSkillMacroForwardedNonFunctionHotkeySlot_ResolvesConfiguredPrimarySlotBinding()
    {
        Func<InputAction, KeyBinding> resolver = BuildBindingResolver(
            new KeyBinding(InputAction.Skill3, Keys.OemOpenBrackets));

        Assert.True(MapSimulator.TryResolveSkillMacroForwardedNonFunctionHotkeySlotForTesting(
            key: Keys.OemOpenBrackets,
            controlHeld: false,
            bindingResolver: resolver,
            out int hotkeySlot));
        Assert.Equal(2, hotkeySlot);
    }

    [Fact]
    public void TryResolveSkillMacroForwardedNonFunctionHotkeySlot_ResolvesConfiguredCtrlSlotBinding()
    {
        Func<InputAction, KeyBinding> resolver = BuildBindingResolver(
            new KeyBinding(InputAction.CtrlSlot2, Keys.OemPlus));

        Assert.True(MapSimulator.TryResolveSkillMacroForwardedNonFunctionHotkeySlotForTesting(
            key: Keys.OemPlus,
            controlHeld: true,
            bindingResolver: resolver,
            out int hotkeySlot));
        Assert.Equal(SkillManager.CTRL_SLOT_OFFSET + 1, hotkeySlot);
    }

    [Fact]
    public void TryResolveSkillMacroForwardedNonFunctionHotkeySlot_FallsBackToDigitLane()
    {
        Assert.True(MapSimulator.TryResolveSkillMacroForwardedNonFunctionHotkeySlotForTesting(
            key: Keys.D4,
            controlHeld: false,
            bindingResolver: null,
            out int hotkeySlot));
        Assert.Equal(3, hotkeySlot);
    }

    private static Func<InputAction, KeyBinding> BuildBindingResolver(params KeyBinding[] bindings)
    {
        Dictionary<InputAction, KeyBinding> byAction = new();
        for (int i = 0; i < bindings.Length; i++)
        {
            KeyBinding binding = bindings[i];
            byAction[binding.Action] = binding;
        }

        return action => byAction.TryGetValue(action, out KeyBinding binding) ? binding : null;
    }
}
