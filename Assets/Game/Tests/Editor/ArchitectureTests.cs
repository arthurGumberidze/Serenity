using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace Game.Tests
{
    public class ArchitectureTests
    {
        private static readonly Dictionary<string, string[]> Dependencies = new Dictionary<string, string[]>
        {
            ["Game.Domain"] = Array.Empty<string>(),
            ["Game.Simulation"] = new[] { "Game.Domain" },
            ["Game.ECS"] = new[] { "Game.Domain", "Game.Simulation" },
            ["Game.Presentation"] = new[] { "Game.Domain", "Game.Simulation" },
            ["Game.Infrastructure"] = new[] { "Game.Domain", "Game.Simulation", "Game.ECS", "Game.Presentation" }
        };

        [Test]
        public void PlayerAssembliesCompileWithOnlyApprovedDependencies()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (var pair in Dependencies)
            {
                var assembly = assemblies.Single(a => a.name == pair.Key);
                Assert.That(assembly.sourceFiles, Is.Not.Empty, pair.Key);
                Assert.That(File.Exists(assembly.outputPath), Is.True, pair.Key);
                Assert.That(assembly.assemblyReferences.Select(a => a.name)
                    .Where(Dependencies.ContainsKey),
                    Is.EquivalentTo(pair.Value), pair.Key);
                Assert.That(assembly.flags.HasFlag(AssemblyFlags.EditorAssembly), Is.False, pair.Key);
            }
            Assert.That(assemblies.Any(a => a.name == "Game.Tests"), Is.False);
        }

        [TestCase("Game.Domain")]
        [TestCase("Game.Simulation")]
        public void CoreHasNoEngineOrEditorReferences(string name)
        {
            var assembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).Single(a => a.name == name);
            Assert.That(assembly.allReferences.Select(Path.GetFileNameWithoutExtension)
                .Where(n => n.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)), Is.Empty, name);
        }

        [Test]
        public void AllProjectAssemblyDefinitionsAreAcyclicAndLayersAreExplicit()
        {
            var definitions = Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories)
                .Select(p => JsonUtility.FromJson<Definition>(File.ReadAllText(p))).ToDictionary(d => d.name);
            foreach (var name in Dependencies.Keys)
            {
                Assert.That(definitions[name].autoReferenced, Is.False, name);
                Assert.That(definitions[name].allowUnsafeCode, Is.False, name);
            }
            Assert.That(definitions["Game.Domain"].noEngineReferences, Is.True);
            Assert.That(definitions["Game.Simulation"].noEngineReferences, Is.True);
            Assert.That(definitions["Game.Tests"].includePlatforms, Is.EquivalentTo(new[] { "Editor" }));
            var complete = new HashSet<string>();
            foreach (var name in definitions.Keys)
                Visit(name, definitions, new HashSet<string>(), complete);
        }

        private static void Visit(string name, Dictionary<string, Definition> definitions,
            HashSet<string> active, HashSet<string> complete)
        {
            if (complete.Contains(name)) return;
            Assert.That(active.Add(name), Is.True, "Assembly cycle at " + name);
            foreach (var reference in definitions[name].references ?? Array.Empty<string>())
            {
                var dependency = reference.StartsWith("GUID:", StringComparison.Ordinal)
                    ? JsonUtility.FromJson<Definition>(File.ReadAllText(UnityEditor.AssetDatabase.GUIDToAssetPath(reference.Substring(5)))).name
                    : reference;
                if (definitions.ContainsKey(dependency)) Visit(dependency, definitions, active, complete);
            }
            active.Remove(name);
            complete.Add(name);
        }

        [Serializable]
        private class Definition
        {
            public string name;
            public string[] references;
            public bool autoReferenced;
            public bool noEngineReferences;
            public bool allowUnsafeCode;
            public string[] includePlatforms;
        }
    }
}
