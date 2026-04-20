using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public class FieldObjectScriptPublicationParserParityTests
{
    [Fact]
    public void ParseScriptNames_DoesNotLeakMetadataOwnerAlias_WhenWrapperBranchIsRecoverable()
    {
        var startScript = new WzSubProperty("startscript");
        var scriptWrapper = new WzSubProperty("script");
        var alias = new WzSubProperty("q22000s");
        alias.AddProperty(new WzIntProperty("state", 1));
        scriptWrapper.AddProperty(alias);
        startScript.AddProperty(scriptWrapper);

        IReadOnlyList<string> parsed = QuestRuntimeManager.ParseScriptNames(startScript);

        Assert.Contains(parsed, value => string.Equals(value, "q22000s", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parsed, value => string.Equals(value, "startscript", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parsed, value => string.Equals(value, "script", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseScriptNames_DoesNotLeakMetadataWrapperNames_WhenUsingMetadataWrapperOnlyStack()
    {
        var startScript = new WzSubProperty("startscript");
        var scriptWrapper = new WzSubProperty("script");
        var alias = new WzSubProperty("q22000e");
        alias.AddProperty(new WzStringProperty("show", "on"));
        scriptWrapper.AddProperty(alias);
        startScript.AddProperty(scriptWrapper);

        IReadOnlyList<string> parsed = QuestRuntimeManager.ParseScriptNames(startScript);

        Assert.Contains(parsed, value => string.Equals(value, "q22000e", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parsed, value => string.Equals(value, "startscript", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parsed, value => string.Equals(value, "script", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parsed, value => string.Equals(value, "show", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_UsesInheritedDelay_ForNestedWrapperAliasPublications()
    {
        var onUserEnter = new WzSubProperty("onUserEnter");
        onUserEnter.AddProperty(new WzIntProperty("delay", 200));
        onUserEnter.AddProperty(new WzStringProperty("q22000s", "1"));

        var scriptWrapper = new WzSubProperty("script");
        scriptWrapper.AddProperty(new WzIntProperty("delay", 350));
        var nestedAlias = new WzSubProperty("q22000e");
        nestedAlias.AddProperty(new WzStringProperty("state", "true"));
        scriptWrapper.AddProperty(nestedAlias);
        onUserEnter.AddProperty(scriptWrapper);

        IReadOnlyList<FieldObjectScriptPublication> publications =
            FieldObjectScriptPublicationParser.Parse(onUserEnter);

        Assert.Contains(publications, publication =>
            string.Equals(publication.ScriptName, "q22000s", StringComparison.OrdinalIgnoreCase)
            && publication.DelayMs == 200);
        Assert.Contains(publications, publication =>
            string.Equals(publication.ScriptName, "q22000e", StringComparison.OrdinalIgnoreCase)
            && publication.DelayMs == 550);
    }

    [Fact]
    public void ParseActions_NpcActFallsBackToFirstParsedPublication_WhenRawActionStringIsMissing()
    {
        var actionsProperty = new WzSubProperty("1");
        var npcAct = new WzSubProperty("npcAct");
        npcAct.AddProperty(new WzIntProperty("delay", 120));
        var scriptWrapper = new WzSubProperty("script");
        var alias = new WzSubProperty("q9933e");
        alias.AddProperty(new WzIntProperty("state", 1));
        scriptWrapper.AddProperty(alias);
        npcAct.AddProperty(scriptWrapper);
        actionsProperty.AddProperty(npcAct);

        MethodInfo parseActionsMethod = typeof(QuestRuntimeManager)
            .GetMethod("ParseActions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(parseActionsMethod);

        var actions = (QuestActionBundle)parseActionsMethod.Invoke(null, new object[] { actionsProperty });
        Assert.NotNull(actions);
        Assert.Equal("q9933e", actions.NpcActionName);
        Assert.Contains(actions.NpcActionPublications, publication =>
            string.Equals(publication.ScriptName, "q9933e", StringComparison.OrdinalIgnoreCase)
            && publication.DelayMs == 120);
    }
}
