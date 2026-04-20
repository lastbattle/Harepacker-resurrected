using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class CharacterLoaderMorphTemplateCandidateParityTests
{
    [Fact]
    public void EnumerateMorphTemplateCandidates_PrefersPaired100xBeforeFamilyBase()
    {
        IReadOnlyList<int> candidates = CharacterLoader.EnumerateMorphTemplateCandidatesForTesting(
            1003,
            static _ => 0);

        Assert.Equal([1003, 1103, 1000], candidates);
    }

    [Fact]
    public void EnumerateMorphTemplateCandidates_PreservesRecursiveLinkChainOrderPerRoot()
    {
        var links = new Dictionary<int, int>
        {
            [1003] = 1201,
            [1201] = 1202,
            [1202] = 0
        };

        IReadOnlyList<int> candidates = CharacterLoader.EnumerateMorphTemplateCandidatesForTesting(
            1003,
            templateId => links.TryGetValue(templateId, out int linked) ? linked : 0);

        Assert.Equal([1003, 1201, 1202, 1103, 1000], candidates);
    }

    [Fact]
    public void EnumerateMorphTemplateCandidates_DeduplicatesCyclesAcrossRootAndLinkedCandidates()
    {
        var links = new Dictionary<int, int>
        {
            [1003] = 1201,
            [1201] = 1003,
            [1103] = 1003,
            [1000] = 1201
        };

        IReadOnlyList<int> candidates = CharacterLoader.EnumerateMorphTemplateCandidatesForTesting(
            1003,
            templateId => links.TryGetValue(templateId, out int linked) ? linked : 0);

        Assert.Equal([1003, 1201, 1103, 1000], candidates);
    }

    [Fact]
    public void EnumerateMorphTemplateCandidates_ContinuesIntoFamilyBaseWhenNoPairFamilyExists()
    {
        IReadOnlyList<int> candidates = CharacterLoader.EnumerateMorphTemplateCandidatesForTesting(
            2002,
            static _ => 0);

        Assert.Equal([2002, 2000], candidates);
    }
}
