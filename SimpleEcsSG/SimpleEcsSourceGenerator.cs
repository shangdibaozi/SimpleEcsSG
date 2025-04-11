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
                    var namespaceName = NamespaceHelper.GetNamespacePath(symbol.ContainingNamespace);
                    
                   
                    var sourceTextStr = AppendClassBody(codeWriter, namespaceName, workItem.ClassDeclaration.Identifier.ToString(), typeName, syntaxReceiver.CandidateStructWorkItems);
                    var sourceText = SourceText.From(sourceTextStr, System.Text.Encoding.UTF8);
                    context.AddSource(symbol.Name + ".g.cs", sourceText);
                    codeWriter.Clear();
                }
            }

            if (syntaxReceiver.CandidateClasses.Count > 0)
            {
                var hashSys = new HashSet<string>();
                foreach (var classDeclarationSyntax in syntaxReceiver.CandidateClasses)
                {
                    var cname = AddSystem(classDeclarationSyntax, context);
                    if (cname != null)
                    {
                        hashSys.Add(cname);
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
                    codeWriter.EndBlock();
                    var sourceText1 = SourceText.From(codeWriter.ToString(), System.Text.Encoding.UTF8);
                    context.AddSource("SystemHelper.g.cs", sourceText1);
                    codeWriter.Clear();
                }
            }
        }

        private static string AddSystem(ClassDeclarationSyntax classDeclarationSyntax, GeneratorExecutionContext context)
        {
            // 不添加虚类
            var canAdd = !classDeclarationSyntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
            if (canAdd)
            {
                // 获取语义模型
                var semanticModel = context.Compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax) as ITypeSymbol;
                // 确保正确获取IEcsSystem接口的符号，需替换实际命名空间
                var ecsSystemInterface = context.Compilation.GetTypeByMetadataName("SimpleEcs.IEcsSystem");
                if (ecsSystemInterface == null)
                    return null; // 接口不存在于当前上下文中
                // 检查类是否直接或间接实现了IEcsSystem
                bool implementsEcsSystem =
                    classSymbol.AllInterfaces.Contains(ecsSystemInterface, SymbolEqualityComparer.Default);
                if (implementsEcsSystem)
                {
                    // 处理实现了IEcsSystem的情况
                    return classDeclarationSyntax.Identifier.ValueText;
                }
            }
            return null;
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