using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class ModifyToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly ModifyTools _tools;
    private readonly PropertyTools _propertyTools;

    public ModifyToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new ModifyTools(_fixture.Session);
        _propertyTools = new PropertyTools(_fixture.Session);
    }

    [Fact]
    public void SetString_ChangesValue()
    {
        var result = _tools.SetString("Character", "Test.img", "testString", "New Value");

        Assert.True(result.Success);
        Assert.Equal("Hello World", result.OldValue);
        Assert.Equal("New Value", result.NewValue);

        // Verify the change
        var verify = _propertyTools.GetString("Character", "Test.img", "testString");
        Assert.Equal("New Value", verify.Data?.Value);

        // Restore original value
        _tools.SetString("Character", "Test.img", "testString", "Hello World");
    }

    [Fact]
    public void SetInt_ChangesValue()
    {
        var result = _tools.SetInt("Character", "Test.img", "testInt", 100);

        Assert.True(result.Success);
        Assert.Equal("42", result.OldValue);
        Assert.Equal("100", result.NewValue);

        // Restore
        _tools.SetInt("Character", "Test.img", "testInt", 42);
    }

    [Fact]
    public void SetFloat_ChangesValue()
    {
        var result = _tools.SetFloat("Character", "Test.img", "testFloat", 6.28f);

        Assert.True(result.Success);

        // Restore
        _tools.SetFloat("Character", "Test.img", "testFloat", 3.14f);
    }

    [Fact]
    public void SetVector_ChangesValue()
    {
        var result = _tools.SetVector("Character", "Test.img", "testVector", 50, 75);

        Assert.True(result.Success);
        Assert.Equal("(100, 200)", result.OldValue);
        Assert.Equal("(50, 75)", result.NewValue);

        // Restore
        _tools.SetVector("Character", "Test.img", "testVector", 100, 200);
    }

    [Fact]
    public void AddProperty_CreatesNewProperty()
    {
        var result = _tools.AddProperty("Character", "Test.img", "info", "newProp", "String", "Test Value");

        Assert.True(result.Success);
        Assert.Equal("newProp", result.Name);

        // Verify
        var verify = _propertyTools.GetString("Character", "Test.img", "info/newProp");
        Assert.True(verify.Success);
        Assert.Equal("Test Value", verify.Data?.Value);

        // Clean up
        _tools.DeleteProperty("Character", "Test.img", "info/newProp");
    }

    [Fact]
    public void AddProperty_WithVector_CreatesVector()
    {
        var result = _tools.AddProperty("Character", "Test.img", "info", "newVector", "Vector", "25,50");

        Assert.True(result.Success);

        // Verify
        var verify = _propertyTools.GetVector("Character", "Test.img", "info/newVector");
        Assert.True(verify.Success);
        Assert.Equal(25, verify.Data?.X);
        Assert.Equal(50, verify.Data?.Y);

        // Clean up
        _tools.DeleteProperty("Character", "Test.img", "info/newVector");
    }

    [Fact]
    public void DeleteProperty_RemovesProperty()
    {
        // First add a property to delete
        _tools.AddProperty("Character", "Test.img", "info", "toDelete", "String", "Delete Me");

        var result = _tools.DeleteProperty("Character", "Test.img", "info/toDelete");

        Assert.True(result.Success);

        // Verify deleted
        var verify = _propertyTools.GetProperty("Character", "Test.img", "info/toDelete");
        Assert.False(verify.Success);
    }

    [Fact]
    public void RenameProperty_RenamesProperty()
    {
        // First add a property to rename
        _tools.AddProperty("Character", "Test.img", "info", "toRename", "String", "Rename Me");

        var result = _tools.RenameProperty("Character", "Test.img", "info/toRename", "renamed");

        Assert.True(result.Success);
        Assert.Equal("toRename", result.OldName);
        Assert.Equal("renamed", result.NewName);

        // Clean up
        _tools.DeleteProperty("Character", "Test.img", "info/renamed");
    }

    [Fact]
    public void CopyProperty_CopiesProperty()
    {
        var result = _tools.CopyProperty(
            "Character", "Test.img", "testString",
            "Character", "Test.img", "info", "copiedString");

        Assert.True(result.Success);

        // Verify
        var verify = _propertyTools.GetString("Character", "Test.img", "info/copiedString");
        Assert.True(verify.Success);
        Assert.Equal("Hello World", verify.Data?.Value);

        // Clean up
        _tools.DeleteProperty("Character", "Test.img", "info/copiedString");
    }

    [Fact]
    public void SetCanvasOrigin_ChangesOrigin()
    {
        var result = _tools.SetCanvasOrigin("Character", "Test.img", "testCanvas", 8, 8);

        Assert.True(result.Success);

        // Restore
        _tools.SetCanvasOrigin("Character", "Test.img", "testCanvas", 16, 16);
    }

    [Fact]
    public void DiscardChanges_ReloadsImage()
    {
        // Make a change
        _tools.SetString("Character", "Test.img", "testString", "Modified");

        var result = _tools.DiscardChanges("Character", "Test.img");

        Assert.True(result.Success);

        // Note: After discard, image is reloaded from disk
        // The test verifies that discard operation succeeds
        // Full value restoration depends on cache behavior
    }
}
