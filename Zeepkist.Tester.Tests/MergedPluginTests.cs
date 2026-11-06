using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// The plugin a run deploys, with the parser and YamlDotNet merged in. Only its metadata is read: its build of YamlDotNet is for the game.
/// </summary>
public sealed class MergedPluginTests : IDisposable
{
    private readonly PEReader _file = new PEReader(File.OpenRead(Path.Combine("MergedPlugin", "Zeepkist.Tester.dll")));

    private MetadataReader Metadata => _file.GetMetadataReader();

    public void Dispose()
    {
        _file.Dispose();
    }

    [Fact]
    public void ItReferencesNeitherYamlDotNetNorTheParser_ButStillTheSdk()
    {
        var references = Metadata.AssemblyReferences.Select(handle => Metadata.GetString(Metadata.GetAssemblyReference(handle).Name)).ToList();

        Assert.DoesNotContain("YamlDotNet", references);
        Assert.DoesNotContain("Zeepkist.Tester.Scenarios", references);
        Assert.Contains("Zeepkist.Tester.Sdk", references);
    }

    [Theory]
    [InlineData("YamlDotNet.RepresentationModel", "YamlStream")]
    [InlineData("TNRD.Zeepkist.Tester.Scenarios", "ScenarioParser")]
    public void WhatsMergedIn_IsInternal_SoNoOtherModCanSeeIt(string typeNamespace, string name)
    {
        Assert.Equal(TypeAttributes.NotPublic, Visibility(typeNamespace, name));
    }

    [Fact]
    public void ThePlugin_StaysPublic_ForBepInEx()
    {
        Assert.Equal(TypeAttributes.Public, Visibility("TNRD.Zeepkist.Tester", "Plugin"));
    }

    private TypeAttributes Visibility(string typeNamespace, string name)
    {
        var definitions = Metadata.TypeDefinitions.Select(Metadata.GetTypeDefinition);
        var type = definitions.Single(definition => Metadata.GetString(definition.Namespace) == typeNamespace && Metadata.GetString(definition.Name) == name);
        return type.Attributes & TypeAttributes.VisibilityMask;
    }
}
