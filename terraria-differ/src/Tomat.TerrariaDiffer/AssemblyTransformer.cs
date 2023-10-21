﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Tomat.TerrariaDiffer;

public static class AssemblyTransformer {
    public static void TransformAssembliesForAssembly(string assemblyPath) {
        var assemblyDir = Path.GetDirectoryName(assemblyPath);

        var resolver = new UniversalAssemblyResolver();
        resolver.AddSearchDirectory(assemblyDir!);
        var assembly = ModuleDefinition.ReadModule(
            assemblyPath,
            new ReaderParameters {
                AssemblyResolver = resolver,
            }
        );
        resolver.AddEmbeddedAssembliesFrom(assembly);

        var assemblyReferences = assembly.AssemblyReferences.Select(x => resolver.Resolve(x)).ToList();
        var assembliesToWrite = new Dictionary<string, AssemblyDefinition>();

        foreach (var assemblyReference in assemblyReferences) {
            if (TransformAssembly(assemblyReference.MainModule))
                assembliesToWrite.Add(assemblyReference.MainModule.FileName, assemblyReference);
        }

        if (TransformAssembly(assembly.Assembly.MainModule))
            assembliesToWrite.Add(assembly.FileName, assembly.Assembly);

        var streams = new Dictionary<string, MemoryStream>();

        foreach (var assemblyReference in assembliesToWrite) {
            var stream = new MemoryStream();
            assemblyReference.Value.Write(stream);
            var path = assemblyReference.Value.MainModule.FileName;
            if (string.IsNullOrEmpty(path))
                path = assemblyReference.Value.Name.Name + ".dll";
            streams.Add(Path.GetFileName(assemblyReference.Value.MainModule.FileName), stream);
        }

        foreach (var assemblyReference in assemblyReferences)
            assemblyReference.Dispose();
        assembly.Dispose();
        // resolver.Dispose();

        foreach (var (fileName, stream) in streams) {
            stream.Position = 0;
            using var fileStream = File.OpenWrite(Path.Combine(assemblyDir!, fileName));
            stream.CopyTo(fileStream);
        }
    }

    private static bool TransformAssembly(ModuleDefinition assembly) {
        var editsMade = false;

        if (assembly.Name == "mscorlib.dll") {
            if (assembly.GetType("System.MathF") is { } mathF) {
                editsMade |= mathF.Fields.Remove(mathF.Fields.FirstOrDefault(x => x.Name == "PI"));
            }
        }

        return editsMade;
    }
}
