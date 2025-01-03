# Abstract
Some dependencies exist at compile time only, namely:
1. Dependencies on constant values (`enum` or `const`)
1. Dependencies through the `nameof` operator.

These dependencies cannot be identified by examining the binary code, because they are inlined by the compiler. On the other hand, the Roslyn semantic model does specify them and so by using it we can detect these compile time dependencies.

This repository demonstrates a bug in the semantic model, where it fails to provide the dependency information for one particular scenarion - `nameof` operator referencing a symbol imported into the scope with `using static`. It works fine for constant values imported into the scope, the defect seems to only exist when `nameof` is involved.

# Demo
```
C:\work\RoslynSemanticModelNameOfBug [master ≡]> dotnet run
--- Type1.cs ---
public class Type1
{
    public const int I = 1;
    public static void f(){}
}

--- Type2.cs ---
using static Type1;
using Type3 = Type1;
public class Type2
{
    public static string X = nameof(f);
    public static int Y = I;
    public static string Z = nameof(Type1.f);
    public static string T = nameof(Type3.f);
}

AssertNameOfDependency("Type2.cs", "nameof(Type1.f)")
AssertNameOfDependency("Type2.cs", "nameof(Type3.f)")
AssertConstValueDependency("Type2.cs", "Y")
AssertNameOfDependency("Type2.cs", "nameof(f)")
Unhandled exception. NUnit.Framework.AssertionException:   Assert.That(nameofOp.Argument.ChildOperations, Has.Count.EqualTo(1))
  Expected: property Count equal to 1
  But was:  0

   at NUnit.Framework.Assert.ReportFailure(String message)
   at NUnit.Framework.Assert.ReportFailure(ConstraintResult result, String message, String actualExpression, String constraintExpression)
   at NUnit.Framework.Assert.That[TActual](TActual actual, IResolveConstraint expression, NUnitString message, String actualExpression, String constraintExpression)
   at Program.AssertNameOfDependency(SemanticModel model, IdentifierNameSyntax nameOfNode, String expectedNameOfSyntax) in C:\work\RoslynSemanticModelNameOfBug\Program.cs:line 87
   at Program.Main() in C:\work\RoslynSemanticModelNameOfBug\Program.cs:line 56
C:\work\RoslynSemanticModelNameOfBug [master ≡]>
```
As one can see, the semantic model correctly handles `Z = nameof(Type1.f)` and `T = nameof(Type3.f)`, but fails to handle `X = nameof(f)`, even though all 3 constructs denote exactly the same thing - the name of the `Type1.f` function. The only difference is how the function identifier is specified:
1. `Type1.f` - fully qualified through its parent type `Type1`
1. `Type3.f` - fully qualified through an alias `Type3`
1. `f` - unqualified, imported into the scope by the `using static Type1` directive.

The semantic model fails to provide the details of the dependency in the last case. However, it does not mean it fails to handle identifiers imported into the scope through `using static`. The code assigns the `Type1.I` constant value to `Type2.Y` and the semantic model has no problem to identify this dependency.