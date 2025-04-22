using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimpleEcsSourceGenerator
{
    [Generator]
    public class SimpleEcsSourceGenerator : ISourceGenerator
    {
        private readonly StringBuilder parameterBuilder = new StringBuilder();
        private readonly StringBuilder parameterBuilderNoType = new StringBuilder();
        private readonly List<string> methods = new List<string>();
        private readonly List<string> methodParams = new List<string>();
        private readonly List<string> methodParamNoType = new List<string>();
        
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new CustomSyntaxReceiver());
        }
        
        public void Execute(GeneratorExecutionContext context)
        {
            var syntaxReceiver = context.SyntaxReceiver as CustomSyntaxReceiver;
            if (syntaxReceiver == null)
            {
                return;
            }
            
            var codeWriter = new CodeWriter();
            foreach (var workItems in syntaxReceiver.CandidateClassWorkItems.Values)
            {
                var workItem = workItems[0];
                var semanticModel = context.Compilation.GetSemanticModel(workItem.ClassDeclaration.SyntaxTree);
                if (ModelExtensions.GetDeclaredSymbol(semanticModel, workItem.ClassDeclaration) is INamedTypeSymbol symbol && symbol != null)
                {
                    var typeName = WriteTypeName(workItem.ClassDeclaration);
                    var namespaceName = GetNamespacePath(symbol.ContainingNamespace);
                    
                   
                    var sourceTextStr = AppendClassBody(codeWriter, namespaceName, workItem.ClassDeclaration.Identifier.ToString(), typeName, syntaxReceiver.CandidateStructWorkItems);
                    var sourceText = SourceText.From(sourceTextStr, System.Text.Encoding.UTF8);
                    context.AddSource(symbol.Name + ".g.cs", sourceText);
                    codeWriter.Clear();
                }
            }

            if (syntaxReceiver.CandidateClasses.Count > 0)
            {
                var observerDict = new Dictionary<INamedTypeSymbol, HashSet<string>>();
                var iEcsSystem = context.Compilation.GetTypeByMetadataName("SimpleEcs.IEcsSystem");
                var iObserver = context.Compilation.GetTypeByMetadataName("SimpleEcs.IObserver");
                var hashSys = new HashSet<string>();
                foreach (var classDeclarationSyntax in syntaxReceiver.CandidateClasses)
                {
                    var isAbstractClass =
                        classDeclarationSyntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
                    if (isAbstractClass)
                    {
                        continue;
                    }

                    var semanticModel = context.Compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                    if (classSymbol == null)
                    {
                        continue;
                    }

                    var isImplementIEcsSystem =
                        classSymbol.AllInterfaces.Contains(iEcsSystem, SymbolEqualityComparer.Default);
                    if (isImplementIEcsSystem)
                    {
                        foreach (var ifc in classSymbol.Interfaces)
                        {
                            if (ifc.AllInterfaces.Contains(iObserver, SymbolEqualityComparer.Default))
                            {
                                if (!observerDict.TryGetValue(ifc, out var systemNames))
                                {
                                    observerDict[ifc] = new HashSet<string>();
                                    systemNames = observerDict[ifc];
                                }

                                systemNames.Add(classDeclarationSyntax.Identifier.ValueText);
                            }
                        }

                        hashSys.Add(classDeclarationSyntax.Identifier.ValueText);
                    }
                }

                if (hashSys.Count > 0)
                {
                    codeWriter.AppendLine();
                    codeWriter.AppendLine("using SimpleEcs;");
                    codeWriter.AppendLine();
                    codeWriter.AppendLine("public static class SystemHelper");
                    codeWriter.BeginBlock();
                    codeWriter.AppendLine("public static EcsSystemGroup CreateRootSystem()");
                    codeWriter.BeginBlock();
                    codeWriter.AppendLine("return new EcsSystemGroup()");
                    foreach (var item in hashSys)
                    {
                        codeWriter.AppendLine($"    .Add<{item}>()");
                    }

                    codeWriter.AppendLine(";");
                    codeWriter.EndBlock();
                    
                    
                    // 事件相关
                    foreach (var item in observerDict)
                    {
                        var ifc = item.Key;
                        var sysNames = item.Value;
                        foreach (var member in ifc.GetMembers())
                        {
                            if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                            {
                                for(var i = 0; i < method.Parameters.Length; i++)
                                {
                                    var param = method.Parameters[i];
                                    parameterBuilder.Append(param.Type.ToDisplayString());
                                    
                                    parameterBuilder.Append($" {param.Name}");
                                    parameterBuilderNoType.Append($"{param.Name}");
                                    if (i != method.Parameters.Length - 1)
                                    {
                                        parameterBuilder.Append(", ");
                                        parameterBuilderNoType.Append(", ");
                                    }
                                }
                                methods.Add(method.Name);
                                methodParams.Add(parameterBuilder.ToString());
                                methodParamNoType.Add(parameterBuilderNoType.ToString());
                                parameterBuilder.Clear();
                                parameterBuilderNoType.Clear();
                            }
                        }

                        for (var j = 0; j < methods.Count; j++)
                        {
                            codeWriter.AppendLine($"public static void {methods[j]}({methodParams[j]})");
                            codeWriter.BeginBlock();
                            foreach (var sysName in sysNames)
                            {
                                codeWriter.AppendLine($"EcsSystemGroup.Sys<{sysName}>().{methods[j]}({methodParamNoType[j]});");
                            }
                            codeWriter.EndBlock();
                            codeWriter.AppendLine();
                        }
                        
                        
                        methods.Clear();
                        methodParams.Clear();
                        methodParamNoType.Clear();
                    }
                    
                    codeWriter.EndBlock();
                    var sourceText1 = SourceText.From(codeWriter.ToString(), System.Text.Encoding.UTF8);
                    context.AddSource("SystemHelper.g.cs", sourceText1);
                    codeWriter.Clear();
                }
            }
        }

        private static void AppendIAspect(in CodeWriter codeWriter, string typeName)
        {
            codeWriter.AppendLine();
            codeWriter.AppendLine($"public interface I{typeName}");
            codeWriter.BeginBlock();
            codeWriter.EndBlock();
        } 
        
        public static string WriteTypeName(TypeDeclarationSyntax typeDeclarationSyntax)
        {
            var typeNameBuilder = new StringBuilder();
        
            typeNameBuilder.Append("public partial ")
                .Append(typeDeclarationSyntax.Keyword.ValueText)
                .Append(" ")
                .Append(typeDeclarationSyntax.Identifier.ToString())
                ;
            return typeNameBuilder.ToString();
        }
        
        private static string AppendClassBody(in CodeWriter codeWriter, string namespaceName, string className, string typeName, Dictionary<string, List<StructWorkItem>> workItems)
        {
            codeWriter.AppendLine();
            codeWriter.AppendLine("using UnityEngine;");
            codeWriter.AppendLine("using SimpleEcs;");
            AppendIAspect(codeWriter, className);
            codeWriter.AppendLine();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                codeWriter.AppendLine("namespace " + namespaceName);
                codeWriter.BeginBlock();
            }
            codeWriter.AppendLine(typeName);
            codeWriter.BeginBlock();

            var hashFiled = new HashSet<string>();
            var dictTag = new Dictionary<string, bool>();
            var interfaceName = $"I{className}";
            foreach (var workItem in workItems.Values)
            {
                foreach (var item in workItem)
                {
                    if (item.ImplementInterfaces.Contains(interfaceName))
                    {
                        hashFiled.Add(item.TypeName);
                        dictTag[item.TypeName] = item.IsTagComponent;
                    }
                }
            }
            
            // 防止重复生成
            foreach (var classTypeName in hashFiled)
            {
                var fileName = FirstCharToLower(classTypeName);
                fileName = fileName.Replace("Component", "Pool").Replace("Comp", "Pool");
                if (dictTag[classTypeName])
                {
                    codeWriter.AppendLine($"public readonly CTagPool<{classTypeName}> {fileName} = null;");
                }
                else
                {
                    codeWriter.AppendLine($"public readonly CPool<{classTypeName}> {fileName} = null;");
                }
            }
        
            codeWriter.EndBlock();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                codeWriter.EndBlock();
            }
            
            
            return codeWriter.ToString();
        }
        
        public static string FirstCharToLower(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return char.ToLowerInvariant(input[0]) + input.Substring(1);
        }
        
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
}