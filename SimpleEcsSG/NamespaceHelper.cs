using Microsoft.CodeAnalysis;

public static class NamespaceHelper
{
    public static string GetNamespacePath(INamespaceSymbol namespaceSymbol)
    {
        if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace)
        {
            return string.Empty;
        }
        
        var parentPath = GetNamespacePath(namespaceSymbol.ContainingNamespace);
        string currentName = namespaceSymbol.Name;

        if (!string.IsNullOrEmpty(parentPath))
        {
            return parentPath + "." + currentName;
        }
        return currentName;
    }
}