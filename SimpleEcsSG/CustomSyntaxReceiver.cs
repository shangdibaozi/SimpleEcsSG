using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class CustomSyntaxReceiver : ISyntaxReceiver
{
    public Dictionary<string, List<ClassWorkItem>> CandidateClassWorkItems { get; } = new Dictionary<string, List<ClassWorkItem>>();
    public Dictionary<string, List<StructWorkItem>> CandidateStructWorkItems { get; } = new Dictionary<string, List<StructWorkItem>>();

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

    private static void TryGetWorkItem(SyntaxNode syntaxNode, out ClassWorkItem classWorkItem, out StructWorkItem structWorkItem)
    {
        classWorkItem = null;
        structWorkItem = null;
        if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax &&
            classDeclarationSyntax.AttributeLists.Count > 0)
        {
            var attributes = from attributeList in classDeclarationSyntax.AttributeLists
                from attribute in attributeList.Attributes
                select attribute;
            var item = new ClassWorkItem(classDeclarationSyntax);
            foreach (var attribute in attributes)
            {
                var attributeName = attribute.Name.ToString();
                switch (attributeName)
                {
                    case var name when name == Def.Attribute_Aspect ||
                                       name == $"{Def.Attribute_Aspect}Attribute" ||
                                       name == Def.NS + "." +
                                       Def.Attribute_Aspect ||
                                       name == Def.NS + "." +
                                       Def.Attribute_Aspect + "Attribute":
                        item.SetIsExist(true);
                        break;
                }
            }

            if (item.IsExist)
            {
                var typeDeclarationSyntax = item.ClassDeclaration as TypeDeclarationSyntax;
                var typeName = new StringBuilder()
                    .Append("partial ")
                    .Append(typeDeclarationSyntax.Keyword.ValueText)
                    .Append(" ")
                    .Append(typeDeclarationSyntax.Identifier.ToString())
                    .Append(typeDeclarationSyntax.TypeParameterList)
                    .Append(" ")
                    .Append(typeDeclarationSyntax.ConstraintClauses.ToString());
                item.SetTypeName(typeName.ToString());
                classWorkItem = item;
            }
        }
        else if (syntaxNode is StructDeclarationSyntax structDeclaration)
        {
            if (structDeclaration.BaseList != null)
            {
                var item = new StructWorkItem(structDeclaration);
                var typeDeclaration = structDeclaration as TypeDeclarationSyntax;
                item.SetTypeName(typeDeclaration.Identifier.ValueText);
                structWorkItem = item;
                
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
    public bool IsExist { get; private set; }
    public string TypeName { get; private set; }

    public ClassWorkItem(ClassDeclarationSyntax classDeclaration)
    {
        ClassDeclaration = classDeclaration;
    }

    public void SetTypeName(string typeName)
    {
        TypeName = typeName;
    }

    public void SetIsExist(bool isExist)
    {
        IsExist = isExist;
    }
}

public class StructWorkItem
{
    public readonly StructDeclarationSyntax StructDeclaration;
    public readonly List<string> ImplementInterfaces = new List<string>();
    public string TypeName { get; private set; }

    public StructWorkItem(StructDeclarationSyntax structDeclaration)
    {
        StructDeclaration = structDeclaration;
    }

    public void SetTypeName(string typeName)
    {
        TypeName = typeName;
    }
}