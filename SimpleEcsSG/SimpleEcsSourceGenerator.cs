using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimpleEcsSourceGenerator
{
    [Generator]
    public class SimpleEcsSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new CustomSyntaxReceiver());
        }
        
        public void Execute(GeneratorExecutionContext context)
        {
            var syntaxReceiver = context.SyntaxReceiver as CustomSyntaxReceiver;
            if (syntaxReceiver == null || syntaxReceiver.CandidateClassWorkItems.Count == 0)
            {
                return;
            }
            
            var codeWriter = new CodeWriter();
            foreach (var workItems in syntaxReceiver.CandidateClassWorkItems.Values)
            {
                var workItem = workItems[0];
                var semanticModel = context.Compilation.GetSemanticModel(workItem.ClassDeclaration.SyntaxTree);
                if (semanticModel.GetDeclaredSymbol(workItem.ClassDeclaration) is INamedTypeSymbol symbol && symbol != null)
                {
                    var typeName = WriteTypeName(workItem.ClassDeclaration);
                    var namespaceName = NamespaceHelper.GetNamespacePath(symbol.ContainingNamespace);
                    
                   
                    var sourceTextStr = AppendClassBody(codeWriter, namespaceName, workItem.ClassDeclaration.Identifier.ToString(), typeName, syntaxReceiver.CandidateStructWorkItems);
                    var sourceTExt1 = SourceText.From(sourceTextStr, System.Text.Encoding.UTF8);
                    context.AddSource(symbol.Name + ".g.cs", sourceTExt1);
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
                        dictTag[item.TypeName] = item.HasFiled;
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
                    codeWriter.AppendLine($"public readonly CPool<{classTypeName}> {fileName} = null;");
                }
                else
                {
                    codeWriter.AppendLine($"public readonly CTagPool<{classTypeName}> {fileName} = null;");
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

    }
}