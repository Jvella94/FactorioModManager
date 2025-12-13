using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System;
using System.IO;
using System.Collections.Generic;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var solutionDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        Console.WriteLine($"Running unused usings remover in: {solutionDir}");

        // Find all .csproj files
        var projects = Directory.EnumerateFiles(solutionDir, "*.csproj", SearchOption.AllDirectories).ToList();
        if (projects.Count == 0)
        {
            Console.WriteLine("No projects found.");
            return 1;
        }

        foreach (var projPath in projects)
        {
            Console.WriteLine($"Opening project: {projPath}");
            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projPath);

            foreach (var document in project.Documents)
            {
                try
                {
                    var root = await document.GetSyntaxRootAsync();
                    var oldText = root!.GetText().ToString();

                    // Simplify (remove unnecessary usings) and format document
                    var simplifiedDoc = await Simplifier.ReduceAsync(document, Simplifier.Annotation);
                    var formattedDoc = await Formatter.FormatAsync(simplifiedDoc);

                    var newRoot = await formattedDoc.GetSyntaxRootAsync();
                    var newText = newRoot!.GetText().ToString();

                    if (newText != oldText)
                    {
                        if (!string.IsNullOrEmpty(document.FilePath))
                        {
                            Console.WriteLine($"Updating {document.FilePath}");
                            File.WriteAllText(document.FilePath, newText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {document.FilePath}: {ex.Message}");
                }
            }
        }

        Console.WriteLine("Done.");
        return 0;
    }
}
