using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using NUnit.Framework;
using System.Reflection;

internal class Program
{
    private const string CODE_1 = @"public class Type1
{
    public const int I = 1;
    public static void f(){}
}
";
    private const string CODE_2 = @"using static Type1;
using Type3 = Type1;
public class Type2
{
    public static string X = nameof(f);
    public static int Y = I;
    public static string Z = nameof(Type1.f);
    public static string T = nameof(Type3.f);
}
";
    private static readonly string[] s_netCoreReferences =
    [
        typeof(object).Assembly.Location,
        Path.GetDirectoryName(typeof(object).Assembly.Location) + "\\System.Runtime.dll",
    ];

    private static void Main()
    {
        SyntaxTree[] syntaxTrees = [CSharpSyntaxTree.ParseText(CODE_1, path: "Type1.cs"), CSharpSyntaxTree.ParseText(CODE_2, path: "Type2.cs")];
        DumpSyntaxTree(syntaxTrees[0]);
        DumpSyntaxTree(syntaxTrees[1]);
        var compilation = CSharpCompilation.Create("temp", syntaxTrees,
            s_netCoreReferences.Select(o => MetadataReference.CreateFromFile(o)).ToArray(),
            new CSharpCompilationOptions(OutputKind.NetModule));
        
        Assert.That(compilation.GetDiagnostics(), Is.Empty);

        var model = compilation.GetSemanticModel(syntaxTrees[1]);
        var nameOfNodes = syntaxTrees[1]
            .GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(o => o.Identifier.ValueText == "nameof")
            .ToList();

        AssertNameOfDependency(model, nameOfNodes[1], "nameof(Type1.f)");
        AssertNameOfDependency(model, nameOfNodes[2], "nameof(Type3.f)");
        AssertConstValueDependency(model, syntaxTrees[1], "Y");

        AssertNameOfDependency(model, nameOfNodes[0], "nameof(f)"); // <---- The problem is right here!!!
    }

    private static void DumpSyntaxTree(SyntaxTree syntaxTree)
    {
        Console.WriteLine($"--- {syntaxTree.FilePath} ---");
        Console.WriteLine(syntaxTree);
    }

    private static void AssertConstValueDependency(SemanticModel model, SyntaxTree syntaxTree, string varName)
    {
        Console.WriteLine($"{MethodBase.GetCurrentMethod()!.Name}(\"{syntaxTree.FilePath}\", \"{varName}\")");
        var syntaxNode = syntaxTree
            .GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(o => o.Declaration.Variables[0].Identifier.ValueText == varName)
            .Select(o => o.Declaration.Variables[0].Initializer!.Value)
            .First();
        var fieldOp = (IFieldReferenceOperation)model.GetOperation(syntaxNode)!;

        Assert.That(fieldOp.Field.ContainingType.Name, Is.EqualTo("Type1"));
        Assert.That(fieldOp.Field.Name, Is.EqualTo("I"));
    }

    private static void AssertNameOfDependency(SemanticModel model, IdentifierNameSyntax nameOfNode, string expectedNameOfSyntax)
    {
        Console.WriteLine($"{MethodBase.GetCurrentMethod()!.Name}(\"{nameOfNode.SyntaxTree.FilePath}\", \"{expectedNameOfSyntax}\")");
        var nameofOp = (INameOfOperation)model.GetOperation(nameOfNode.Parent!)!;
        Assert.That(nameofOp.Syntax.ToString(), Is.EqualTo(expectedNameOfSyntax));
        Assert.That(nameofOp.Argument.Type, Is.Null);
        Assert.That(nameofOp.Argument.ChildOperations, Has.Count.EqualTo(1));
        var op = nameofOp.Argument.ChildOperations.First();
        Assert.That(op.Type!.Name, Is.EqualTo("Type1"));
    }
}