using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class CustomSyntaxReceiver : ISyntaxReceiver
{
    public Dictionary<string, List<ClassWorkItem>> CandidateClassWorkItems { get; } = new Dictionary<string, List<ClassWorkItem>>();
    public Dictionary<string, List<StructWorkItem>> CandidateStructWorkItems { get; } = new Dictionary<string, List<StructWorkItem>>();
    
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        TryGetWorkItem(syntaxNode, out var classWorkItem, out var structWorkItem);
        if (classWorkItem != null)
        {
            if (CandidateClassWorkItems.TryGetValue(classWorkItem.TypeName, out var existingList))
            {
                existingList.Add(classWorkItem);
            }
            else
            {
                CandidateClassWorkItems.Add(classWorkItem.TypeName, new List<ClassWorkItem> { classWorkItem });
            }
        }

        if (structWorkItem != null)
        {
            if (CandidateStructWorkItems.TryGetValue(structWorkItem.TypeName, out var existingList))
            {
                existingList.Add(structWorkItem);
            }
            else
            {
                CandidateStructWorkItems.Add(structWorkItem.TypeName, new List<StructWorkItem> { structWorkItem });
            }
        }
    }

    private void TryGetWorkItem(SyntaxNode syntaxNode, out ClassWorkItem classWorkItem, out StructWorkItem structWorkItem)
    {
        classWorkItem = null;
        structWorkItem = null;
        if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax)
        {
            if (classDeclarationSyntax.BaseList == null)
            {
                return;
            }

            var isAspect = false;
            foreach (var baseType in classDeclarationSyntax.BaseList.Types)
            {
                var className = baseType.Type.ToString();
                if (className.Equals($"Aspect<{classDeclarationSyntax.Identifier.ValueText}>"))
                {
                    var classItem = new ClassWorkItem(classDeclarationSyntax);
                    classItem.SetTypeName(classDeclarationSyntax.Identifier.ValueText);
                    classWorkItem = classItem;
                    isAspect = true;
                }
            }

            if (!isAspect)
            {
                if (!classDeclarationSyntax.Identifier.ValueText.Equals("EcsSystemGroup"))
                {
                    CandidateClasses.Add(classDeclarationSyntax);
                }
            }
        }
        else if (syntaxNode is StructDeclarationSyntax structDeclaration)
        {
            if (structDeclaration.BaseList != null)
            {
                var item = new StructWorkItem(structDeclaration);
                item.SetTypeName(structDeclaration.Identifier.ValueText);
                structWorkItem = item;

                var hasFiled = structDeclaration.Members.Any(member => 
                    member is FieldDeclarationSyntax || 
                    member is PropertyDeclarationSyntax);

                structWorkItem.IsTagComponent = !hasFiled && structDeclaration.Identifier.ValueText.StartsWith("Tag");
                
                foreach (var baseType in structDeclaration.BaseList.Types)
                {
                    var typeName = baseType.Type.ToString();
                    item.ImplementInterfaces.Add(typeName);
                }
            }
        }
    }
}

public class ClassWorkItem
{
    public readonly ClassDeclarationSyntax ClassDeclaration;
    public string TypeName { get; private set; }

    public ClassWorkItem(ClassDeclarationSyntax classDeclaration)
    {
        ClassDeclaration = classDeclaration;
    }

    public void SetTypeName(string typeName)
    {
        TypeName = typeName;
    }
}

public class StructWorkItem
{
    public readonly StructDeclarationSyntax StructDeclaration;
    public readonly List<string> ImplementInterfaces = new List<string>();
    public string TypeName { get; private set; }

    public bool IsTagComponent = false;

    public StructWorkItem(StructDeclarationSyntax structDeclaration)
    {
        StructDeclaration = structDeclaration;
    }

    public void SetTypeName(string typeName)
    {
        TypeName = typeName;
    }
}