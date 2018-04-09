using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageAssembly : PackageFile
    {
        readonly Lazy<AssemblyDefinition> definition;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> idecompiler;
        private readonly IAssemblyResolver resolver;

        public AssemblyDefinition Definition => definition.Value;

        public PackageAssembly (ZipArchiveEntry entry, IAssemblyResolver resolver)
            : base (entry)
        {
            this.resolver = resolver;
            var isBuild = entry.FullName.StartsWith ("build/");
            definition = new Lazy<AssemblyDefinition> (() => {
                if (ArchiveEntry == null)
                    return null;
                var ms = new MemoryStream ((int)ArchiveEntry.Length);
                using (var es = ArchiveEntry.Open ()) {
                    es.CopyTo (ms);
                    ms.Position = 0;
                }
                return AssemblyDefinition.ReadAssembly (ms, new ReaderParameters {
                    AssemblyResolver = resolver,
                });
            }, true);
            var format = ICSharpCode.Decompiler.CSharp.OutputVisitor.FormattingOptionsFactory.CreateMono ();
            format.SpaceBeforeMethodCallParentheses = false;
            format.SpaceBeforeMethodDeclarationParentheses = false;
            format.SpaceBeforeConstructorDeclarationParentheses = false;
            idecompiler = new Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> (() => {
                var m = Definition?.MainModule;
                if (m == null)
                    return null;
                return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler (m, new ICSharpCode.Decompiler.DecompilerSettings {
                    ShowXmlDocumentation = false,
                    ThrowOnAssemblyResolveErrors = false,
                    AlwaysUseBraces = false,
                    CSharpFormattingOptions = format,
                    ExpandMemberDefinitions = false,
                    DecompileMemberBodies = false,
                    UseExpressionBodyForCalculatedGetterOnlyProperties = true,
                });
            }, true);
            decompiler = new Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> (() => {
                var m = Definition?.MainModule;
                if (m == null)
                    return null;
                return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler (m, new ICSharpCode.Decompiler.DecompilerSettings {
                    ShowXmlDocumentation = false,
                    ThrowOnAssemblyResolveErrors = false,
                    AlwaysUseBraces = false,
                    CSharpFormattingOptions = format,
                    ExpandMemberDefinitions = true,
                    DecompileMemberBodies = true,
                    UseExpressionBodyForCalculatedGetterOnlyProperties = true,
                });
            }, true);
        }

        public Task<string> GetTypeCodeAsync (TypeDefinition type)
        {
            return Task.Run (() => {
                try {
                    var d = decompiler.Value;
                    if (d == null)
                        return "// No decompiler available";
                    return d.DecompileTypeAsString (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (type.FullName));
                }
                catch (Exception e) {
                    return "/* " + e.Message + " */";
                }
            });
        }

        public Task<string> GetTypeInterfaceCodeAsync (TypeDefinition type)
        {
            return Task.Run (() => {
                try {
                    var d = idecompiler.Value;
                    if (d == null)
                        return "// No decompiler available";
                    return d.DecompileTypeAsString (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (type.FullName));
                }
                catch (Exception e) {
                    return "/* " + e.Message + " */";
                }
            });
        }
    }
}
