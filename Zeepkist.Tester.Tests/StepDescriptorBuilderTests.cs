using System;
using System.Linq;
using System.Text.RegularExpressions;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepDescriptorBuilderTests
{
    [Fact]
    public void Build_StepWithoutArguments_HasItsNameAndNoArguments()
    {
        var descriptor = Build(typeof(TestSteps.Undo));

        Assert.Equal("undo", descriptor.Name);
        Assert.Equal(string.Empty, descriptor.Namespace);
        Assert.Equal("undo", descriptor.FullName);
        Assert.Empty(descriptor.Arguments);
    }

    [Fact]
    public void Build_PrimaryScalarArgument_IsNamedInKebabCaseAndRequired()
    {
        var descriptor = Build(typeof(TestSteps.Wait));

        var seconds = Assert.Single(descriptor.Arguments);
        Assert.Equal("seconds", seconds.Name);
        Assert.Equal(ArgumentKind.Seconds, seconds.Type.Kind);
        Assert.True(seconds.IsPrimary);
        Assert.True(seconds.IsRequired);
        Assert.Null(seconds.Default);
    }

    [Fact]
    public void Build_PrimaryAndOptionalArguments_DescribesEachOfThem()
    {
        var descriptor = Build(typeof(TestSteps.PlaceBlock));

        Assert.Equal(["function", "as", "how-many", "offset"], descriptor.Arguments.Select(argument => argument.Name));

        var function = descriptor.Arguments[0];
        Assert.Equal(ArgumentKind.String, function.Type.Kind);
        Assert.True(function.IsPrimary);
        Assert.True(function.IsRequired);
        Assert.Equal("^[a-z_]+$", function.Pattern);

        var name = descriptor.Arguments[1];
        Assert.Equal(ArgumentKind.String, name.Type.Kind);
        Assert.False(name.IsPrimary);
        Assert.False(name.IsRequired);
        Assert.Null(name.Default);

        var count = descriptor.Arguments[2];
        Assert.Equal(ArgumentKind.Int, count.Type.Kind);
        Assert.False(count.IsRequired);
        Assert.Equal(1, count.Default);
        Assert.Equal(1, count.Minimum);
        Assert.Equal(8, count.Maximum);

        var offset = descriptor.Arguments[3];
        Assert.Equal(ArgumentKind.Pixels, offset.Type.Kind);
        Assert.False(offset.IsRequired);
    }

    [Fact]
    public void Build_StepWithExamples_ListsThemInOrder()
    {
        var descriptor = Build(typeof(TestSteps.PlaceBlock));

        Assert.Equal(["- place-block: lamp", "- place-block: { function: lamp, as: my-lamp }"], descriptor.Examples);
    }

    [Fact]
    public void Build_PrimaryListOfNestedClasses_DescribesTheItemsArguments()
    {
        var descriptor = Build(typeof(TestSteps.DragWindowEdge));

        Assert.Equal("zls", descriptor.Namespace);
        Assert.Equal("zls.drag-window-edge", descriptor.FullName);

        var moves = descriptor.Arguments[0];
        Assert.Equal("moves", moves.Name);
        Assert.True(moves.IsPrimary);
        Assert.True(moves.IsRequired);
        Assert.Equal(1, moves.MinItems);
        Assert.Equal(ArgumentKind.List, moves.Type.Kind);

        var item = moves.Type.ItemType;
        Assert.Equal(ArgumentKind.Object, item.Kind);
        Assert.Equal(["by", "over"], item.Arguments.Select(argument => argument.Name));

        var by = item.Arguments[0];
        Assert.Equal(ArgumentKind.Pixels, by.Type.Kind);
        Assert.True(by.IsPrimary);
        Assert.True(by.IsRequired);

        var over = item.Arguments[1];
        Assert.Equal(ArgumentKind.Int, over.Type.Kind);
        Assert.False(over.IsPrimary);
        Assert.Equal(1, over.Default);
        Assert.Equal(1, over.Minimum);
        Assert.Equal(600, over.Maximum);

        var sideways = descriptor.Arguments[1];
        Assert.Equal(ArgumentKind.Bool, sideways.Type.Kind);
        Assert.Equal(false, sideways.Default);
    }

    [Fact]
    public void Build_Enum_ListsItsValuesAsAScenarioWritesThem()
    {
        var axis = Argument(typeof(TestSteps.EveryType), "axis");

        Assert.Equal(ArgumentKind.Enum, axis.Type.Kind);
        Assert.Equal(["horizontal", "global_velocity", "page-up"], axis.Type.EnumValues);
    }

    [Fact]
    public void Build_EnumDefault_IsTheValueAsAScenarioWritesIt()
    {
        var axis = Argument(typeof(TestSteps.EveryType), "axis");

        Assert.Equal("global_velocity", axis.Default);
    }

    [Fact]
    public void Build_NullableEnum_IsAnEnumToo()
    {
        var axis = Argument(typeof(TestSteps.EveryType), "maybe-axis");

        Assert.Equal(ArgumentKind.Enum, axis.Type.Kind);
        Assert.False(axis.IsRequired);
    }

    [Theory]
    [InlineData("text", ArgumentKind.TextBlock)]
    [InlineData("float", ArgumentKind.Float)]
    [InlineData("bool", ArgumentKind.Bool)]
    [InlineData("keys", ArgumentKind.Keys)]
    [InlineData("file-name", ArgumentKind.FileName)]
    public void Build_ScalarArgument_HasItsKind(string name, ArgumentKind kind)
    {
        Assert.Equal(kind, Argument(typeof(TestSteps.EveryType), name).Type.Kind);
    }

    [Fact]
    public void Build_ListOfScalars_HasItsItemType()
    {
        var times = Argument(typeof(TestSteps.EveryType), "times");

        Assert.Equal(ArgumentKind.List, times.Type.Kind);
        Assert.Equal(ArgumentKind.Seconds, times.Type.ItemType.Kind);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("ctrl+space escape", true)]
    [InlineData("ctrl+/", true)]
    [InlineData("ctrl+\\", true)]
    [InlineData("shift+alt+pagedown", true)]
    [InlineData("-", true)]
    [InlineData("", false)]
    [InlineData("ctrl+", false)]
    [InlineData("hyper+a", false)]
    [InlineData("ctrl+a  b", false)]
    [InlineData("spacebar", false)]
    [InlineData("Ctrl+A", false)]
    public void Build_Keys_TakesTheKeyChordSyntax(string keys, bool matches)
    {
        var pattern = Argument(typeof(TestSteps.EveryType), "keys").Pattern;

        Assert.Equal(matches, Regex.IsMatch(keys, pattern));
    }

    [Fact]
    public void Build_ListOfKeys_HasThePatternOnItsItems()
    {
        var chords = Argument(typeof(TestSteps.EveryType), "chords");

        Assert.Equal(Keys.Pattern, chords.Type.ItemType.Pattern);
    }

    [Theory]
    [InlineData("500ms", true)]
    [InlineData("0.5s", true)]
    [InlineData("500", false)]
    [InlineData("-1s", false)]
    [InlineData("1m", false)]
    public void Build_Seconds_TakesTheTextFormAsAPattern(string seconds, bool matches)
    {
        var pattern = Argument(typeof(TestSteps.EveryType), "timeout").Pattern;

        Assert.Equal(matches, Regex.IsMatch(seconds, pattern));
    }

    [Theory]
    [InlineData("running_2-b", true)]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("a.png", false)]
    public void Build_FileName_TakesASafeName(string name, bool matches)
    {
        var pattern = Argument(typeof(TestSteps.EveryType), "file-name").Pattern;

        Assert.Equal(matches, Regex.IsMatch(name, pattern));
    }

    [Fact]
    public void Build_Name_RecordsThatItDefinesAValueOfItsKind()
    {
        var defines = Argument(typeof(TestSteps.Named), "defines");

        Assert.Equal(ArgumentKind.Name, defines.Type.Kind);
        Assert.Equal("block", defines.Type.ValueKind);
        Assert.Equal("Uid", defines.Type.Substitute);
    }

    [Fact]
    public void Build_NullableName_IsANameToo()
    {
        var defines = Argument(typeof(TestSteps.Named), "maybe-defines");

        Assert.Equal(ArgumentKind.Name, defines.Type.Kind);
        Assert.Equal("window", defines.Type.ValueKind);
        Assert.False(defines.IsRequired);
    }

    [Fact]
    public void Build_Ref_RecordsThatItUsesAValueOfItsKind()
    {
        var uses = Argument(typeof(TestSteps.Named), "uses");

        Assert.Equal(ArgumentKind.Ref, uses.Type.Kind);
        Assert.Equal("block", uses.Type.ValueKind);
    }

    [Fact]
    public void Build_KindWithoutSubstitute_HasNone()
    {
        var items = Argument(typeof(TestSteps.Named), "uses-each").Type.ItemType;

        Assert.Equal(ArgumentKind.Ref, items.Kind);
        Assert.Equal("window", items.ValueKind);
        Assert.Null(items.Substitute);
    }

    [Theory]
    [InlineData("defines", "lamp_2-b", true)]
    [InlineData("defines", "", false)]
    [InlineData("defines", "my lamp", false)]
    [InlineData("defines", "${lamp}", false)]
    [InlineData("uses", "lamp_2-b", true)]
    [InlineData("uses", "my lamp", false)]
    public void Build_NameAndRef_TakeASafeName(string argument, string name, bool matches)
    {
        var pattern = Argument(typeof(TestSteps.Named), argument).Pattern;

        Assert.Equal(matches, Regex.IsMatch(name, pattern));
    }

    [Fact]
    public void Build_WithSummaries_DocumentsTheStepAndItsArguments()
    {
        var builder = AsTester(XmlDocs.Read([typeof(TestSteps).Assembly.Location]));

        var descriptor = builder.Build(typeof(TestSteps.Wait));

        Assert.Equal("Waits, such as for the level to run a little before a screenshot.", descriptor.Documentation);
        Assert.Equal("How long to wait.", Assert.Single(descriptor.Arguments).Documentation);
    }

    [Fact]
    public void Build_WithSummaries_DocumentsNestedClassesAndTheirArguments()
    {
        var builder = AsTester(XmlDocs.Read([typeof(TestSteps).Assembly.Location]));

        var item = builder.Build(typeof(TestSteps.DragWindowEdge)).Arguments[0].Type.ItemType;

        Assert.Equal("One move of a drag.", item.Documentation);
        Assert.Equal("Frames the move takes.", item.Arguments[1].Documentation);
    }

    [Fact]
    public void Build_WithoutSummaries_HasNoDocumentation()
    {
        var descriptor = Build(typeof(TestSteps.Wait));

        Assert.Null(descriptor.Documentation);
        Assert.Null(Assert.Single(descriptor.Arguments).Documentation);
    }

    private static ArgumentDescriptor Argument(Type stepType, string name)
    {
        return Build(stepType).Arguments.Single(argument => argument.Name == name);
    }

    private static StepDescriptor Build(Type stepType)
    {
        return AsTester(_ => null).Build(stepType);
    }

    /// <summary>
    /// The builder as if this assembly were the tester's, so its steps may use the reserved empty namespace.
    /// </summary>
    private static StepDescriptorBuilder AsTester(Func<string, string> summaries)
    {
        return new StepDescriptorBuilder { TesterAssemblyName = typeof(TestSteps).Assembly.GetName().Name, Summaries = summaries };
    }
}
