using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool TryShowMiniRoomWindow(out string restrictionMessage, bool inheritDirectionModeOwner = false)
        {
            restrictionMessage = GetSocialRoomRestrictionMessage(SocialRoomKind.MiniRoom);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                PushFieldRuleMessage(restrictionMessage, Environment.TickCount, showOverlay: false);
                return false;
            }

            WireMiniRoomWindowData();
            if (inheritDirectionModeOwner)
            {
                ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MiniRoom);
            }
            else
            {
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MiniRoom);
            }
            return true;
        }

        private bool TryShowSocialRoomWindow(SocialRoomKind kind, out string restrictionMessage, bool inheritDirectionModeOwner = false)
        {
            restrictionMessage = GetSocialRoomRestrictionMessage(kind);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                PushFieldRuleMessage(restrictionMessage, Environment.TickCount, showOverlay: false);
                return false;
            }

            string windowName = GetSocialRoomWindowName(kind);
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return false;
            }

            if (kind == SocialRoomKind.MiniRoom)
            {
                return TryShowMiniRoomWindow(out restrictionMessage, inheritDirectionModeOwner);
            }

            WireSocialRoomWindow(windowName, uiWindowManager?.InventoryWindow as InventoryUI);
            if (inheritDirectionModeOwner)
            {
                ShowWindowWithInheritedDirectionModeOwner(windowName);
            }
            else
            {
                ShowDirectionModeOwnedWindow(windowName);
            }
            return true;
        }

        private string ShowSocialRoomWindowForCallback(SocialRoomKind kind, string openedMessage)
        {
            return TryShowSocialRoomWindow(kind, out string restrictionMessage, inheritDirectionModeOwner: true)
                ? openedMessage
                : restrictionMessage;
        }

        private string GetSocialRoomRestrictionMessage(SocialRoomKind kind)
        {
            if (kind is not (SocialRoomKind.MiniRoom or SocialRoomKind.PersonalShop or SocialRoomKind.EntrustedShop))
            {
                return null;
            }

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            return FieldInteractionRestrictionEvaluator.GetMiniGameRestrictionMessage(fieldLimit);
        }

        private string GetPetFieldRestrictionMessage()
        {
            return FieldInteractionRestrictionEvaluator.GetPetRuntimeRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0)
                ?? _playerManager?.Pets?.FieldUsageRestrictionMessage;
        }

        private bool TryShowFieldRestrictedWindow(string windowName)
        {
            string restrictionMessage = GetFieldWindowRestrictionMessage(windowName);
            if (string.IsNullOrWhiteSpace(restrictionMessage))
            {
                return true;
            }

            PushFieldRuleMessage(restrictionMessage, Environment.TickCount, showOverlay: false);
            return false;
        }

        private bool TryOpenFieldRestrictedWindow(string windowName, bool inheritDirectionModeOwner = false, Action beforeShow = null)
        {
            if (!TryShowFieldRestrictedWindow(windowName))
            {
                return false;
            }

            beforeShow?.Invoke();
            if (inheritDirectionModeOwner)
            {
                ShowWindowWithInheritedDirectionModeOwner(windowName);
            }
            else
            {
                ShowDirectionModeOwnedWindow(windowName);
            }

            return true;
        }

        private string GetFieldWindowRestrictionMessage(string windowName)
        {
            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            return windowName switch
            {
                MapSimulatorWindowNames.MemoMailbox or MapSimulatorWindowNames.QuestDelivery =>
                    FieldInteractionRestrictionEvaluator.GetParcelOpenRestrictionMessage(fieldLimit),
                MapSimulatorWindowNames.QuestAlarm =>
                    FieldInteractionRestrictionEvaluator.GetQuestAlertRestrictionMessage(fieldLimit),
                _ => null
            };
        }

        private void HandlePlayerLanding(PlayerCharacter player, PlayerLandingInfo landingInfo)
        {
            if (player == null || !player.IsAlive)
            {
                return;
            }

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            FallDamageResult fallDamage = FieldFallingDamageEvaluator.Evaluate(
                player.MaxHP,
                landingInfo.FallStartY,
                landingInfo.LandingY,
                landingInfo.ImpactVelocityY,
                !FieldInteractionRestrictionEvaluator.CanTakeFallingDamage(fieldLimit));

            if (!fallDamage.ShouldApply)
            {
                return;
            }

            player.TakeDamage(fallDamage.Damage, 0f, 0f);
        }

        private void ApplyFieldRuntimeInteractionRestrictions()
        {
            PetController pets = _playerManager?.Pets;
            if (pets != null)
            {
                string petRestrictionMessage = FieldInteractionRestrictionEvaluator.GetPetRuntimeRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);
                pets.SetFieldUsageRestriction(!string.IsNullOrWhiteSpace(petRestrictionMessage), petRestrictionMessage);
            }

            if (uiWindowManager?.EquipWindow is EquipUIBigBang equipBigBang)
            {
                equipBigBang.SetAndroidPaneAvailable(FieldInteractionRestrictionEvaluator.CanUseAndroid(_mapBoard?.MapInfo?.fieldLimit ?? 0));
            }
        }

        private void ApplyFieldRuntimeMinimapRestrictions()
        {
            if (miniMapUi == null)
            {
                return;
            }

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            if (FieldInteractionRestrictionEvaluator.ShouldAutoExpandMinimap(fieldLimit))
            {
                miniMapUi.EnsureExpanded();
            }
        }
    }
}
