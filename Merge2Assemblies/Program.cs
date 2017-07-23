using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy;
using log4net;
using log4net.Config;
using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Baml2006;
using System.Xaml;

namespace Merge2Assemblies
{
    class Program
    {
        private const string FILTER = "Genesys*.dll";
        private const string TAB = "\t";
        private const string BAML_EXTENSION = ".baml";

        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        private static Dictionary<string, List<string>> bufferInfo = new Dictionary<string, List<string>>();

        private static string[] MyInterfaces = new string[]
                {
                    "IMainLoginViewModel",
                    "IStaticCaseDataViewModel",
                    "IStaticCaseDataView",
                    "IConfigurationService",
                    "IPagePlaceView",
                    "IInteractionBarInteractionView",
                    "IAboutWindow",
                    "IAlertMessageMenu",
                    "IGenericDataView",
                    "IDispositionCodeView",
                    "IDispositionCodeViewModel",
                    "IMainBundleView",
                    "INotepadView",
                    "MainWindowEventHandler",
                    "IInteractionBarCaseView",
                    "IMyBroadcastMessageView",
                    "IBundleToolbarView",
                    "IRescheduleToolbarView",
                    "IWorkbinsView",
                    "SIPMonitor",
                    "IOutboundRecordView",
                    "IPartyView",
                    "IInteractionWorkitemToolbarView",
                    "IInteractionInboundEmailToolbarView",
                    "IInteractionOutboundEmailToolbarView",
                    "ICaseDataView"
                };

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            string oldFolder = @"C:\";
            string newFolder = @"C:\";
            DirectoryInfo oldDi = new DirectoryInfo(oldFolder);
            DirectoryInfo newDi = new DirectoryInfo(newFolder);
            if (oldDi.Exists && newDi.Exists)
            {
                FileInfo[] oldGenDll = oldDi.GetFiles(FILTER);
                FileInfo[] newGenDll = newDi.GetFiles(FILTER);

                if (oldGenDll.Length != newGenDll.Length)
                {
                    log.Info(string.Format("oldGenDll.Length = {0}, newGenDll.Length = {1}", oldGenDll.Length, newGenDll.Length));

                    // Compare names:
                    throw new NotImplementedException();
                }

                foreach (FileInfo fiOld in oldGenDll)
                {
                    FileInfo fiNew = newGenDll.Where(t => t.Name.Equals(fiOld.Name)).FirstOrDefault();

                    ModuleDefinition oldModule = ModuleDefinition.ReadModule(fiOld.FullName);
                    var oldTypes = oldModule.Types;
                    ModuleDefinition newModule = ModuleDefinition.ReadModule(fiNew.FullName);
                    var newTypes = newModule.Types;

                    if (oldTypes.Count != newTypes.Count)
                    {
                        Append2Dictionary(fiOld.Name, string.Format("oldTypes.Count = {0}, newTypes.Count = {1}", oldTypes.Count, newTypes.Count));

                        // Compare names:
                        var oldNames = oldTypes.Select(t => t.Name).ToList();
                        var newNames = newTypes.Select(t => t.Name).ToList();

                        CompareNames(fiOld.Name, oldNames, newNames, "Types Added", "Types Removed");
                    }

                    foreach (TypeDefinition oldType in oldTypes)
                    {
                        if (!oldType.IsInterface && !IsClassImplementInterfaceInList(oldType, MyInterfaces))
                            continue;

                        TypeDefinition newType = newTypes.Where(t => t.Name.Equals(oldType.Name)).FirstOrDefault();
                        if (newType == null) continue;

                        //if (!MyInterfaces.Contains(oldType.Name)) 
                        //    continue;

                        var allOldMethods = oldType.Methods;
                        var allnewMethods = newType.Methods;

                        if (allOldMethods.Count != allnewMethods.Count)
                        {
                            Append2Dictionary(oldType.Name, string.Format("allOldMethods.Count = {0}, allnewMethods.Count = {1}", allOldMethods.Count, allnewMethods.Count));

                            // Compare names:
                            var oldNames = allOldMethods.Select(t => t.Name).ToList();
                            var newNames = allnewMethods.Select(t => t.Name).ToList();
                            CompareNames(oldType.Name, oldNames, newNames, "Methods Added", "Methods Removed");

                        }

                        foreach (MethodDefinition oldMdef in allOldMethods)
                        {
                            MethodDefinition newMdef = allnewMethods.Where(t => t.Name.Equals(oldMdef.Name)).FirstOrDefault();
                            if (newMdef == null) continue;

                            string cSharpCodeDisAssembOld = getSourceCode(oldMdef);
                            if (string.IsNullOrWhiteSpace(cSharpCodeDisAssembOld))
                                continue;
                            string cSharpCodeDisAssembNew = getSourceCode(newMdef);
                            if (cSharpCodeDisAssembOld != cSharpCodeDisAssembNew)
                            {
                                Append2Dictionary(oldType.Name, TAB + oldMdef.Name + " are differents.");
                            }
                        }
                    }


                    // xaml
                    if (oldModule.Resources.Count > 0)
                    {

                        EmbeddedResource rOld = (EmbeddedResource)oldModule.Resources.Where(r => r.Name.Contains(".g.")).FirstOrDefault();
                        ResourceReader readerOld = null;
                        using (var stream = rOld.GetResourceStream())
                        {
                            using (readerOld = new ResourceReader(stream))
                            {
                                EmbeddedResource rNew = (EmbeddedResource)newModule.Resources.Where(r => r.Name.Contains(".g.")).FirstOrDefault();
                                ResourceReader readerNew = null;
                                using (var stream2 = rNew.GetResourceStream())
                                {
                                    using (readerNew = new ResourceReader(stream2))
                                    {

                                        foreach (DictionaryEntry entry in readerOld)
                                        {
                                            ///Read the values in the entry
                                            if (!entry.Key.ToString().Contains(BAML_EXTENSION)) continue;
                                            string fullName = entry.Key.ToString();
                                            int indexOfLastSlash = fullName.LastIndexOf(@"/");
                                            int indexOfDotBaml = fullName.IndexOf(BAML_EXTENSION);
                                            string className = entry.Key.ToString().Substring(indexOfLastSlash + 1, indexOfDotBaml - indexOfLastSlash - 1);

                                            var classOfThisBaml = oldTypes.Where(t => t.Name.ToLower().Equals(className)).FirstOrDefault();

                                            if (classOfThisBaml == null || (!classOfThisBaml.IsInterface && !IsClassImplementInterfaceInList(classOfThisBaml, MyInterfaces)))
                                                continue;

                                            //var newClassOfThisBaml = newTypes.Where(t => t.Name.ToLower().Equals(className)).FirstOrDefault();
                                            System.IO.MemoryStream codeofOldBaml = entry.Value as MemoryStream;
                                            string oldCode = string.Empty;
                                            using (StreamReader oldStreamR = new StreamReader(codeofOldBaml as Stream, Encoding.UTF8))
                                            {
                                                oldCode = oldStreamR.ReadToEnd();
                                                oldStreamR.Close();
                                            }

                                            Baml2006Reader reader = new Baml2006Reader(codeofOldBaml as Stream);
                                            while (!reader.IsEof)
                                            {
                                                var noeud = reader.Value;
                                                reader.Read();
                                            }
                                            //var writer = new XamlObjectWriter(reader.SchemaContext);
                                            //while (reader.Read())
                                            //    writer.WriteNode(reader);

                                            string typeofNewBaml = string.Empty;
                                            byte[] codeofNewBaml = null;
                                            readerNew.GetResourceData(fullName, out typeofNewBaml, out codeofNewBaml);
                                            // en fait le baml est un code précompilé, reste à trouver comment le décompiler en xaml
                                            string newcode = Encoding.UTF8.GetString(codeofNewBaml);
                                        }
                                        stream2.Close();
                                    }
                                    readerNew.Close();
                                }
                            }
                            readerOld.Close();

                            stream.Close();
                        }
                    }

                    // print
                    //if (bufferInfo.Count > 1)
                    {
                        foreach (var item in bufferInfo)
                        {
                            log.Info(item.Key);
                            foreach (var line in item.Value)
                                log.Info(line);
                        }
                        if (bufferInfo.Count > 0)
                            NewLine();
                    }

                    bufferInfo.Clear();
                }
            }

        }

        private static bool IsClassImplementInterfaceInList(TypeDefinition theType, string[] interfaces)
        {
            foreach (var interf in theType.Interfaces)
            {
                if (interfaces.Contains(interf.Name)) return true;
            }
            return false;
        }



        private static void Append2Dictionary(string key, string s)
        {
            if (!bufferInfo.ContainsKey(key))
            {
                bufferInfo.Add(key, new List<string>());
            }
            bufferInfo[key].Add(s);
        }

        private static void NewLine()
        {
            log.Info("______________________________________________________________");

        }

        private static void CompareNames(string key, List<string> oldNames, List<string> newNames, string addedString, string removedString)
        {
            Except(key, newNames, oldNames, addedString);
            Except(key, oldNames, newNames, removedString);

        }

        private static void Except(string key, List<string> Names1, List<string> Names2, string p, string space = TAB)
        {
            List<String> onlyB = Names1.Except(Names2).ToList();
            if (onlyB != null && onlyB.Count > 0)
            {
                Append2Dictionary(key, space + p);
                foreach (string s in onlyB)
                {
                    Append2Dictionary(key, space + space + s);
                }
            }
        }

        public static string getSourceCode(MethodDefinition methodDefinition)
        {
            try
            {
                var csharpLanguage = new CSharpLanguage();
                var textOutput = new PlainTextOutput();
                var decompilationOptions = new DecompilationOptions();
                decompilationOptions.FullDecompilation = true;
                csharpLanguage.DecompileMethod(methodDefinition, textOutput, decompilationOptions);
                return textOutput.ToString();

            }
            catch (Exception exception)
            {
                log.ErrorFormat("in getSourceCode: {0}", new object[] { exception.Message });
                //return ("Error in creating source code from IL: " + exception.Message);
                return string.Empty;
            }
        }
    }
}
