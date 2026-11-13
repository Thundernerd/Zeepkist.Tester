using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// The <c>&lt;summary&gt;</c>s in the XML doc files the compiler writes next to assemblies, as Markdown.
/// </summary>
internal static class XmlDocs
{
    private static readonly Regex _whitespace = new Regex(@"\s+");

    /// <summary>
    /// Finds a member's summary by the name of its assembly and its XML doc id, such as <c>Mod</c> and <c>T:Mod.Compile</c>.
    /// An assembly without a doc file adds nothing.
    /// </summary>
    /// <remarks>
    /// Two assemblies can have a type of one name, so each doc file's summaries are kept apart, under the assembly it names.
    /// </remarks>
    public static Func<string, string, string> Read(IEnumerable<string> assemblyPaths)
    {
        var summaries = new Dictionary<(string Assembly, string Id), string>();
        foreach (var path in assemblyPaths.Select(assemblyPath => Path.ChangeExtension(assemblyPath, ".xml")).Where(File.Exists))
        {
            var doc = XDocument.Load(path);
            var assembly = doc.Root?.Element("assembly")?.Element("name")?.Value ?? Path.GetFileNameWithoutExtension(path);
            foreach (var member in doc.Descendants("member"))
            {
                var summary = member.Element("summary");
                if (summary != null)
                    summaries[(assembly, member.Attribute("name").Value)] = _whitespace.Replace(ToMarkdown(summary), " ").Trim();
            }
        }

        return (assembly, id) => summaries.GetValueOrDefault((assembly, id));
    }

    /// <summary>
    /// As <see cref="Read(IEnumerable{string})"/>, but reads an assembly's doc file only once a summary from it is asked for,
    /// so it also finds the docs of an assembly that is only referenced, such as one with states a mod's steps name.
    /// </summary>
    /// <param name="locate">The path of the assembly with a name, or null when it can't be found.</param>
    public static Func<string, string, string> ReadWhenAsked(Func<string, string> locate)
    {
        var byAssembly = new Dictionary<string, Func<string, string, string>>();
        return (assembly, id) =>
        {
            if (!byAssembly.TryGetValue(assembly, out var summaries))
            {
                var path = locate(assembly);
                if (path == null)
                    return null;

                summaries = byAssembly[assembly] = Read([path]);
            }

            return summaries(assembly, id);
        };
    }

    private static string ToMarkdown(XElement element)
    {
        return string.Concat(element.Nodes().Select(node => node switch
        {
            XText text => text.Value,
            XElement { Name.LocalName: "c" } code => $"`{code.Value}`",
            XElement { Name.LocalName: "see" } see => SeeToMarkdown(see),
            XElement { Name.LocalName: "paramref" or "typeparamref" } reference => $"`{reference.Attribute("name")?.Value}`",
            XElement other => ToMarkdown(other),
            _ => string.Empty,
        }));
    }

    private static string SeeToMarkdown(XElement see)
    {
        var label = ToMarkdown(see);
        if (see.Attribute("href")?.Value is { } href)
            return $"[{(label.Length > 0 ? label : href)}]({href})";

        if (label.Length > 0)
            return label;

        return $"`{see.Attribute("langword")?.Value ?? ShortName(see.Attribute("cref")?.Value ?? string.Empty)}`";
    }

    /// <summary>
    /// <c>M:Mod.Steps.Wait.Run(System.Int32)</c> to <c>Run</c>, <c>T:Mod.Named`1</c> to <c>Named</c>, and <c>M:Mod.Wait.#ctor</c> to <c>Wait</c>.
    /// </summary>
    private static string ShortName(string cref)
    {
        var name = cref.Split('(')[0].Replace(".#ctor", string.Empty);
        name = name.Substring(Math.Max(name.LastIndexOf('.'), name.LastIndexOf(':')) + 1);
        return name.Split('`')[0];
    }
}
