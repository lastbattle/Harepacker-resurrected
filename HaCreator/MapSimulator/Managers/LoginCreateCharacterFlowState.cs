using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public enum LoginCreateCharacterStage
    {
        RaceSelect = 0,
        JobSelect = 1,
        AvatarSelect = 2,
        NameSelect = 3
    }

    public enum LoginCreateCharacterRaceKind
    {
        Explorer = 0,
        Cygnus = 1000,
        Aran = 2000,
        Evan = 2001,
        Resistance = 3000
    }

    public enum LoginCreateCharacterAvatarPart
    {
        Face = 0,
        Hair = 1,
        HairColor = 2,
        Skin = 3,
        Coat = 4,
        Pants = 5,
        Shoes = 6,
        Weapon = 7
    }

    public sealed class LoginCreateCharacterJobOption
    {
        public LoginCreateCharacterJobOption(string label, short subJob, int beginnerJobId = 0)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Beginner" : label;
            SubJob = subJob;
            BeginnerJobId = beginnerJobId;
        }

        public string Label { get; }
        public short SubJob { get; }
        public int BeginnerJobId { get; }
    }

    public sealed class LoginCreateCharacterRequestProfile
    {
        public LoginCreateCharacterRaceKind Race { get; init; }
        public CharacterGender Gender { get; init; }
        public int BeginnerJobId { get; init; }
        public short SubJob { get; init; }
        public SkinColor Skin { get; init; }
        public int FaceId { get; init; }
        public int HairStyleId { get; init; }
        public int HairColorValue { get; init; }
        public int HairId { get; init; }
        public int CoatId { get; init; }
        public int PantsId { get; init; }
        public int ShoesId { get; init; }
        public int WeaponId { get; init; }

        public LoginNewCharacterRequest ToPacketRequest(string characterName)
        {
            return new LoginNewCharacterRequest(
                characterName?.Trim() ?? string.Empty,
                (int)Race,
                SubJob,
                (byte)Gender,
                FaceId,
                HairStyleId,
                (int)Skin,
                HairColorValue,
                CoatId,
                PantsId,
                ShoesId,
                WeaponId);
        }
    }

    public sealed class LoginCreateCharacterFlowState
    {
        public static readonly LoginCreateCharacterRaceKind[] SupportedRaces =
        {
            LoginCreateCharacterRaceKind.Explorer,
            LoginCreateCharacterRaceKind.Cygnus,
            LoginCreateCharacterRaceKind.Aran,
            LoginCreateCharacterRaceKind.Evan,
            LoginCreateCharacterRaceKind.Resistance
        };

        private readonly Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<LoginCreateCharacterJobOption>> _jobsByRace =
            new()
            {
                [LoginCreateCharacterRaceKind.Explorer] = new[]
                {
                    new LoginCreateCharacterJobOption("Warrior", 0),
                    new LoginCreateCharacterJobOption("Magician", 1),
                    new LoginCreateCharacterJobOption("Bowman", 2),
                    new LoginCreateCharacterJobOption("Thief", 3),
                    new LoginCreateCharacterJobOption("Pirate", 4)
                },
                [LoginCreateCharacterRaceKind.Cygnus] = new[]
                {
                    new LoginCreateCharacterJobOption("Noblesse", 0, 1000)
                },
                [LoginCreateCharacterRaceKind.Aran] = new[]
                {
                    new LoginCreateCharacterJobOption("Legend", 0, 2000)
                },
                [LoginCreateCharacterRaceKind.Evan] = new[]
                {
                    new LoginCreateCharacterJobOption("Evan", 0, 2001)
                },
                [LoginCreateCharacterRaceKind.Resistance] = new[]
                {
                    new LoginCreateCharacterJobOption("Citizen", 0, 3000)
                }
            };

        public LoginCreateCharacterFlowState()
        {
            SelectedRace = LoginCreateCharacterRaceKind.Explorer;
            SelectedGender = CharacterGender.Male;
            Stage = LoginCreateCharacterStage.RaceSelect;
        }

        public LoginCreateCharacterStage Stage { get; private set; }
        public LoginCreateCharacterRaceKind SelectedRace { get; private set; }
        public int SelectedRaceIndex => Array.IndexOf(SupportedRaces, SelectedRace);
        public int SelectedJobIndex { get; private set; }
        public CharacterGender SelectedGender { get; private set; }
        public string EnteredName { get; private set; } = string.Empty;
        public string CheckedName { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;

        public int SelectedFaceIndex { get; private set; }
        public int SelectedHairIndex { get; private set; }
        public int SelectedHairColorIndex { get; private set; }
        public int SelectedSkinIndex { get; private set; }
        public int SelectedCoatIndex { get; private set; }
        public int SelectedPantsIndex { get; private set; }
        public int SelectedShoesIndex { get; private set; }
        public int SelectedWeaponIndex { get; private set; }

        public IReadOnlyList<LoginCreateCharacterJobOption> CurrentJobs =>
            _jobsByRace.TryGetValue(SelectedRace, out IReadOnlyList<LoginCreateCharacterJobOption> jobs)
                ? jobs
                : Array.Empty<LoginCreateCharacterJobOption>();

        public LoginCreateCharacterJobOption SelectedJob =>
            SelectedJobIndex >= 0 && SelectedJobIndex < CurrentJobs.Count
                ? CurrentJobs[SelectedJobIndex]
                : CurrentJobs.FirstOrDefault();

        public void Start()
        {
            Stage = LoginCreateCharacterStage.RaceSelect;
            StatusMessage = "Choose the race family for the new character.";
            CheckedName = string.Empty;
        }

        public void SetStage(LoginCreateCharacterStage stage, string statusMessage = null)
        {
            Stage = stage;
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                StatusMessage = statusMessage;
            }
        }

        public void SelectRace(int raceIndex)
        {
            if (raceIndex < 0 || raceIndex >= SupportedRaces.Length)
            {
                return;
            }

            SelectedRace = SupportedRaces[raceIndex];
            SelectedJobIndex = 0;
            CheckedName = string.Empty;
            StatusMessage = $"Selected {GetRaceLabel(SelectedRace)}.";
        }

        public void SelectJob(int jobIndex)
        {
            if (jobIndex < 0 || jobIndex >= CurrentJobs.Count)
            {
                return;
            }

            SelectedJobIndex = jobIndex;
            StatusMessage = $"Selected {CurrentJobs[jobIndex].Label}.";
        }

        public void ToggleGender()
        {
            SelectedGender = SelectedGender == CharacterGender.Male
                ? CharacterGender.Female
                : CharacterGender.Male;
            ResetAvatarIndices();
            CheckedName = string.Empty;
            StatusMessage = $"Switched to {SelectedGender}.";
        }

        public void RollRandom(CharacterLoader loader)
        {
            CharacterLoader.LoginStarterAvatarCatalog catalog = loader?.GetLoginStarterAvatarCatalog(SelectedRace, SelectedGender);
            if (catalog == null)
            {
                return;
            }

            SelectedFaceIndex = PickRandomIndex(catalog.FaceIds.Count);
            SelectedHairIndex = PickRandomIndex(catalog.HairStyleIds.Count);
            SelectedHairColorIndex = PickRandomIndex(catalog.HairColorIndices.Count);
            SelectedSkinIndex = PickRandomIndex(catalog.Skins.Count);
            SelectedCoatIndex = PickRandomIndex(catalog.CoatIds.Count);
            SelectedPantsIndex = PickRandomIndex(catalog.PantsIds.Count);
            SelectedShoesIndex = PickRandomIndex(catalog.ShoesIds.Count);
            SelectedWeaponIndex = PickRandomIndex(catalog.WeaponIds.Count);
            CheckedName = string.Empty;
            StatusMessage = "Rolled a new starter appearance from MakeCharInfo.";
        }

        public void ShiftAvatarPart(CharacterLoader.LoginStarterAvatarCatalog catalog, LoginCreateCharacterAvatarPart part, int delta)
        {
            if (catalog == null || delta == 0)
            {
                return;
            }

            switch (part)
            {
                case LoginCreateCharacterAvatarPart.Face:
                    SelectedFaceIndex = CycleIndex(SelectedFaceIndex, catalog.FaceIds.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.Hair:
                    SelectedHairIndex = CycleIndex(SelectedHairIndex, catalog.HairStyleIds.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.HairColor:
                    SelectedHairColorIndex = CycleIndex(SelectedHairColorIndex, catalog.HairColorIndices.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.Skin:
                    SelectedSkinIndex = CycleIndex(SelectedSkinIndex, catalog.Skins.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.Coat:
                    SelectedCoatIndex = CycleIndex(SelectedCoatIndex, catalog.CoatIds.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.Pants:
                    SelectedPantsIndex = CycleIndex(SelectedPantsIndex, catalog.PantsIds.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.Shoes:
                    SelectedShoesIndex = CycleIndex(SelectedShoesIndex, catalog.ShoesIds.Count, delta);
                    break;
                case LoginCreateCharacterAvatarPart.Weapon:
                    SelectedWeaponIndex = CycleIndex(SelectedWeaponIndex, catalog.WeaponIds.Count, delta);
                    break;
            }

            CheckedName = string.Empty;
            StatusMessage = "Adjusted the starter appearance.";
        }

        public void SetEnteredName(string enteredName)
        {
            string normalized = (enteredName ?? string.Empty).Trim();
            if (!string.Equals(EnteredName, normalized, StringComparison.Ordinal))
            {
                EnteredName = normalized;
                CheckedName = string.Empty;
            }
        }

        public void AcceptCheckedName(string checkedName)
        {
            EnteredName = (checkedName ?? string.Empty).Trim();
            CheckedName = EnteredName;
            StatusMessage = string.IsNullOrWhiteSpace(CheckedName)
                ? "The duplicate-name packet succeeded without a name."
                : $"{CheckedName} passed duplicate-name validation.";
        }

        public void ClearCheckedName(string statusMessage)
        {
            CheckedName = string.Empty;
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                StatusMessage = statusMessage;
            }
        }

        public bool HasCheckedName =>
            !string.IsNullOrWhiteSpace(CheckedName) &&
            string.Equals(CheckedName, EnteredName, StringComparison.Ordinal);

        public CharacterBuild CreatePreviewBuild(CharacterLoader loader)
        {
            LoginCreateCharacterRequestProfile request = BuildRequestProfile(loader);
            if (request == null)
            {
                return null;
            }

            return loader.LoadLoginStarterBuild(
                request.Gender,
                request.Skin,
                request.FaceId,
                request.HairId,
                request.CoatId,
                request.PantsId,
                request.ShoesId,
                request.WeaponId);
        }

        public LoginCreateCharacterRequestProfile BuildRequestProfile(CharacterLoader loader)
        {
            CharacterLoader.LoginStarterAvatarCatalog catalog = loader?.GetLoginStarterAvatarCatalog(SelectedRace, SelectedGender);
            if (catalog == null)
            {
                return null;
            }

            return new LoginCreateCharacterRequestProfile
            {
                Race = SelectedRace,
                Gender = SelectedGender,
                BeginnerJobId = SelectedJob?.BeginnerJobId ?? 0,
                SubJob = SelectedJob?.SubJob ?? 0,
                Skin = GetValue(catalog.Skins, SelectedSkinIndex, SkinColor.Light),
                FaceId = GetValue(catalog.FaceIds, SelectedFaceIndex, SelectedGender == CharacterGender.Male ? 20000 : 21000),
                HairStyleId = GetValue(catalog.HairStyleIds, SelectedHairIndex, SelectedGender == CharacterGender.Male ? 30000 : 31000),
                HairColorValue = GetValue(catalog.HairColorIndices, SelectedHairColorIndex, 0),
                HairId = loader.ResolveLoginStarterHairId(catalog, SelectedGender, SelectedHairIndex, SelectedHairColorIndex),
                CoatId = GetValue(catalog.CoatIds, SelectedCoatIndex, 1042003),
                PantsId = GetValue(catalog.PantsIds, SelectedPantsIndex, 1062007),
                ShoesId = GetValue(catalog.ShoesIds, SelectedShoesIndex, 1072005),
                WeaponId = GetValue(catalog.WeaponIds, SelectedWeaponIndex, 1322013)
            };
        }

        public void ResetAvatarIndices()
        {
            SelectedFaceIndex = 0;
            SelectedHairIndex = 0;
            SelectedHairColorIndex = 0;
            SelectedSkinIndex = 0;
            SelectedCoatIndex = 0;
            SelectedPantsIndex = 0;
            SelectedShoesIndex = 0;
            SelectedWeaponIndex = 0;
        }

        public static string GetRaceLabel(LoginCreateCharacterRaceKind race)
        {
            return race switch
            {
                LoginCreateCharacterRaceKind.Cygnus => "Cygnus",
                LoginCreateCharacterRaceKind.Aran => "Aran",
                LoginCreateCharacterRaceKind.Evan => "Evan",
                LoginCreateCharacterRaceKind.Resistance => "Resistance",
                _ => "Explorer"
            };
        }

        private static int CycleIndex(int current, int count, int delta)
        {
            if (count <= 0)
            {
                return 0;
            }

            int normalized = current + delta;
            while (normalized < 0)
            {
                normalized += count;
            }

            return normalized % count;
        }

        private static int PickRandomIndex(int count)
        {
            return count <= 1 ? 0 : Random.Shared.Next(count);
        }

        private static T GetValue<T>(IReadOnlyList<T> values, int index, T fallback)
        {
            return values != null && index >= 0 && index < values.Count
                ? values[index]
                : fallback;
        }
    }
}
