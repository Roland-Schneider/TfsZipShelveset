using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.Framework.Client;
using System.IO;
using Microsoft.TeamFoundation.VersionControl.Common;
using System.IO.Compression;
using Microsoft.TeamFoundation;
using System.Text.RegularExpressions;
using System.Configuration;

namespace TfZip
{
    class Program
    {
        private static readonly string ServerFilesZipContentPrefix = @"$\";
        private static readonly string LocalFilesZipContentPrefix = @"@LocalFiles\";

        static void Usage()
        {
            Console.WriteLine(String.Format("Usage:   TfZip [-y] -shel[veset] <shelvesetname>[;<shelvesetowner>] [<zipoutfile>]     Creates a zipfile containing the files in the shelveset and their previous versions."));
            Console.WriteLine(String.Format("                                                                                       A filename will be generated from <shelvesetname> if <zipoutfile> ends with '\\' or is missing."));
            Console.WriteLine(String.Format("                                                                                       '-y' suppresses prompting to confirm overwrite an existing <zipoutfile>."));
            Console.WriteLine(String.Format("         TfZip [-y] -pend[ing] <zipoutfile>                                            Creates a zipfile containing all pending files and their previous versions."));
            Console.WriteLine(String.Format("         TfZip [-y] -wfol[der] <zipoutfile>                                            Creates a zipfile containing all pending files, their previous versions and selected local files according to configured rules."));
            Console.WriteLine(String.Format("         TfZip -conf[ig]                                                               Opens the local files selection rules in the default editor for \".config\" files."));
        }


        enum ItemsSource
        {
            ShelveSet = 1,
            PendingChanges = 2,
            PendingChangesAndWorkfolderLocalFiles = 4,
        }


        static int Main(String[] args)
        {
            System.Reflection.Assembly assembly = typeof(Program).Assembly;
            Version assemblyVersion = typeof(Program).Assembly.GetName().Version;
            System.Runtime.Versioning.TargetFrameworkAttribute targetFramework = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), true).OfType<System.Runtime.Versioning.TargetFrameworkAttribute>().Single();
            Version tfsAssemblyVersion = typeof(TeamFoundationVersion).Assembly.GetName().Version;
            string tfZipVersion = String.Format("{0}.{1}", assemblyVersion.Major, assemblyVersion.Minor);
            Console.WriteLine(String.Format("TfZip {0} (Requires {1}, uses TFS assemblies {2})", tfZipVersion, targetFramework.FrameworkDisplayName, tfsAssemblyVersion));

            String zipFilePath = null;

            ItemsSource itemsSource = (ItemsSource)0;
            String shelveSetSpec = null;
            String shelvesetName = null;
            String shelvesetOwner = null;

            if (args.Length == 0)
            {
                Usage();
                return 3;
            }

            WorkspaceInfo wsInfo = null;
            bool confirmOverwriteOutfile = true;

            System.Collections.IEnumerator enu = args.GetEnumerator();
            enu.Reset();
            while (enu.MoveNext())
            {
                string arg = (string)enu.Current;
                string option = arg.ToLowerInvariant();

                if (String.Equals(option, "-y", StringComparison.InvariantCultureIgnoreCase) || String.Equals(args[0], "/y", StringComparison.InvariantCultureIgnoreCase))
                {
                    confirmOverwriteOutfile = false;
                }
                else if ((option.Length >= 5) && ("-shelveset".StartsWith(option, StringComparison.InvariantCultureIgnoreCase) || "/shelveset".StartsWith(option, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (!enu.MoveNext())
                    {
                        Usage();
                        return 3;
                    }

                    shelveSetSpec = (string)enu.Current;
                    WorkspaceSpec.Parse(shelveSetSpec, RepositoryConstants.AuthenticatedUser, out shelvesetName, out shelvesetOwner);
                    if (String.IsNullOrEmpty(shelvesetName) || String.IsNullOrEmpty(shelvesetOwner))
                    {
                        Usage();
                        return 3;
                    }

                    if (enu.MoveNext())
                    {
                        zipFilePath = (string)enu.Current;
                    }

                    zipFilePath = EnsureValidZipOutFile(zipFilePath,
                                                        delegate()
                                                        { 
                                                            return shelvesetName;
                                                        });

                    itemsSource = ItemsSource.ShelveSet;
                }
                else if ((option.Length >= 5) && ("-pending".StartsWith(option, StringComparison.InvariantCultureIgnoreCase) || "/pending".StartsWith(option, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (enu.MoveNext())
                    {
                        zipFilePath = (string)enu.Current;
                    }

                    try
                    {
                        zipFilePath = EnsureValidZipOutFile(zipFilePath,
                                                            delegate()
                                                            {
                                                                wsInfo = Workstation.Current.GetLocalWorkspaceInfo(Environment.CurrentDirectory);
                                                                if (wsInfo == null)
                                                                {
                                                                    Console.Error.WriteLine("The current directory is not part of a workspace.");
                                                                    throw new ApplicationException();
                                                                }

                                                                string fallbackFileName = DateTime.Now.ToString("yyyyMMddTHHmmss") + '_' + wsInfo.Name + '@' + wsInfo.Computer;
                                                                return fallbackFileName;
                                                            });
                    }
                    catch (ApplicationException)
                    {
                        return 1;
                    }

                    itemsSource = ItemsSource.PendingChanges;
                }
                else if ((option.Length >= 5) && ("-wfolder".StartsWith(option, StringComparison.InvariantCultureIgnoreCase) || "/wfolder".StartsWith(option, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (enu.MoveNext())
                    {
                        zipFilePath = (string)enu.Current;
                    }

                    try
                    {
                        zipFilePath = EnsureValidZipOutFile(zipFilePath,
                                                            delegate()
                                                            {
                                                                wsInfo = Workstation.Current.GetLocalWorkspaceInfo(Environment.CurrentDirectory);
                                                                if (wsInfo == null)
                                                                {
                                                                    Console.Error.WriteLine("The current directory is not part of a workspace.");
                                                                    throw new ApplicationException();
                                                                }

                                                                string fallbackFileName = DateTime.Now.ToString("yyyyMMddTHHmmss") + '_' + wsInfo.Name + '@' + wsInfo.Computer;
                                                                return fallbackFileName;
                                                            });
                    }
                    catch (ApplicationException)
                    {
                        return 1;
                    }

                    Configuration userConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    if (!userConfig.HasFile)
                    {
                        Console.Error.WriteLine("The local files selection configuration is not yet prepared.");
                        Console.Error.WriteLine(String.Format("The configuration file \"{0}\" will be opened using the default editor.", userConfig.FilePath));
                        Console.Error.WriteLine("Please check the selection rules.");
                        TfZipSettings.Default.LocalFilesSelectionConfiguration = TfZipSettings.Default.LocalFilesSelectionConfiguration;
                        TfZipSettings.Default.Save();
                        System.Diagnostics.Process.Start(userConfig.FilePath);
                        return 1;
                    }

                    Expression localFilesSelector = TfZipSettings.Default.LocalFilesSelectionConfiguration;
                    if (localFilesSelector == null)
                    {
                        Console.Error.WriteLine("The local files selection configuration is missing. Please check the selection rules.");
                        System.Diagnostics.Process.Start(userConfig.FilePath);
                        return 1;
                    }

                    Console.Error.WriteLine(String.Format("Using local files selection rules from \"{0}\"", userConfig.FilePath));

                    itemsSource = ItemsSource.PendingChangesAndWorkfolderLocalFiles;
                }
                else if ((option.Length >= 5) && ("-config".StartsWith(option, StringComparison.InvariantCultureIgnoreCase) || "/config".StartsWith(option, StringComparison.InvariantCultureIgnoreCase)))
                {
                    Configuration userConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    if (!userConfig.HasFile)
                    {
                        //Expression readonlyRule = new Expression();
                        //readonlyRule.ExpressionType = "Rule";
                        //readonlyRule.IsReadOnly = String.Empty;

                        //Expression excludeRule = new Expression();
                        //excludeRule.ExpressionType = "Rule";
                        //excludeRule.Not = String.Empty;
                        //excludeRule.FullNameRegex = @"\\bin\\x86\\debug\\";

                        //Expression root = new Expression();
                        //root.ExpressionType = "And";
                        //root.Operands = new ExpressionList() { readonlyRule, excludeRule };
                        //TfZipSettings.Default.LocalFilesSelectionConfiguration = root;

                        TfZipSettings.Default.LocalFilesSelectionConfiguration = TfZipSettings.Default.LocalFilesSelectionConfiguration;
                        TfZipSettings.Default.Save();
                    }
                    System.Diagnostics.Process.Start(userConfig.FilePath);
                    return 0;
                }
                else
                {
                    Usage();
                    return 3;
                }
            }

            if (itemsSource == (ItemsSource)0)
            {
                Usage();
                return 3;
            }


            if (wsInfo == null)
            {
                wsInfo = Workstation.Current.GetLocalWorkspaceInfo(Environment.CurrentDirectory);
                if (wsInfo == null)
                {
                    Console.Error.WriteLine("The current directory is not part of a workspace.");
                    return 1;
                }
            }

            // TFS v2.0:
            // using (TfsTeamProjectCollection tfsServer = new TfsTeamProjectCollection(wsInfo.ServerUri, new UICredentialsProvider()))
            // TFS v4.0:
            using (TfsTeamProjectCollection tfsServer = new TfsTeamProjectCollection(wsInfo.ServerUri, new TfsClientCredentials()))
            {
                tfsServer.EnsureAuthenticated();

                VersionControlServer versionControl = (VersionControlServer)tfsServer.GetService(typeof(VersionControlServer));
                if (versionControl == null)
                {
                    Console.WriteLine(String.Format("Could not access VersionControlServer service."));
                    return 1;
                }

                Shelveset shelveset = null;
                PendingChange[] pendingChanges;
                if ((itemsSource == ItemsSource.PendingChanges) || (itemsSource == ItemsSource.PendingChangesAndWorkfolderLocalFiles))
                {
                    Workspace workspace = versionControl.GetWorkspace(wsInfo);
                    pendingChanges = workspace.GetPendingChanges();
                }
                else if (itemsSource == ItemsSource.ShelveSet)
                {
                    PendingSet[] pendingSets = null;
                    try
                    {
                        pendingSets = versionControl.QueryShelvedChanges(shelvesetName, shelvesetOwner, null, true);
                    }
                    catch (ShelvesetNotFoundException)
                    {
                        Console.WriteLine(String.Format("Could not find shelveset \"{0}\".", shelveSetSpec));
                        return 1;
                    }
                    if ((pendingSets == null) || (pendingSets.Length == 0) || (pendingSets.Length > 1))
                    {
                        throw new InvalidOperationException("Unexpected QueryShelvedChanges result.");
                    }

                    pendingChanges = pendingSets[0].PendingChanges;

                    shelveset = versionControl.QueryShelvesets(shelvesetName, shelvesetOwner).Single();
                }
                else
                {
                    throw new InvalidOperationException();
                }

                FileInfo zipFilePathInfo = new FileInfo(zipFilePath);
                if (zipFilePathInfo.Exists)
                {
                    if (zipFilePathInfo.IsReadOnly)
                    {
                        Console.WriteLine(String.Format("Cannot write to outfile \"{0}\".", zipFilePath));
                        return 1;
                    }
                    else if (confirmOverwriteOutfile)
                    {
                        while (true)
                        {
                            Console.Write(String.Format("Overwrite \"{0}\" ? (Yes/No): ", zipFilePath));
                            string result = Console.ReadLine().ToLowerInvariant();
                            if (result.Length >= 1 && "yes".StartsWith(result))
                            {
                                break;
                            }
                            else if (result.Length >= 1 && "no".StartsWith(result))
                            {
                                return 1;
                            }
                        }
                    }
                }
                else
                {
                    if (!zipFilePathInfo.Directory.Exists)
                    {
                        Console.Error.WriteLine(String.Format("Cannot access outfile path \"{0}\".", zipFilePath));
                        return 1;
                    }
                }

                string zipFilePathFull = zipFilePathInfo.FullName;

                using (FileStream zipToOpen = new FileStream(zipFilePathFull, FileMode.Create))
                {
                    using (ZipArchive zipArchive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    {
                        string tmpFile = System.IO.Path.GetTempPath() + "TfZip.tmp";
                        FileInfo tmpFileInfo = new FileInfo(tmpFile);
                        try
                        {
                            if (tmpFileInfo.Exists)
                            {
                                tmpFileInfo.Delete();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(String.Format("Cannot use temporary storage file \"{0}\"." + Environment.NewLine + "{1}", tmpFileInfo, ex.Message));
                            return 1;
                        }

                        MemoryStream pendingChangesInfo = new MemoryStream();
                        StreamWriter pendingChangesInfoWriter = new StreamWriter(pendingChangesInfo);

                        if (pendingChanges.Length == 0)
                        {
                            Console.Error.WriteLine("No pending changes. No file written.");
                            return 1;
                        }

                        foreach (PendingChange pendingChange in pendingChanges)
                        {
                            int pendingChangeBaseVersion = pendingChange.Version;
                            string basedOn = (pendingChangeBaseVersion > 0) ? String.Format("C{0}", pendingChange.Version) : String.Empty;
                            string originalPath = pendingChange.IsRename ? pendingChange.SourceServerItem : null;
                            string originalPathInfo = originalPath != null ? String.Format("{0}", originalPath) : String.Empty;
                            string changeType = PendingChange.GetLocalizedStringForChangeType(pendingChange.ChangeType);
                            string info = String.Format("{0}|{1}|{2}|{3}", pendingChange.ServerItem, changeType, basedOn, originalPathInfo);
                            Console.WriteLine(info);
                            pendingChangesInfoWriter.WriteLine(info);

                            if (pendingChange.ItemType != ItemType.File)
                            {
                                continue;
                            }

                            string serverItemRelativePath = pendingChange.ServerItem.Substring(2).Replace('/', Path.DirectorySeparatorChar);

                            if (!pendingChange.IsDelete)
                            {
                                if (pendingChange.LocalOrServerItem.StartsWith("$"))
                                {
                                    pendingChange.DownloadShelvedFile(tmpFile);
                                }
                                else
                                {
                                    string path = Path.GetDirectoryName(tmpFile);
                                    Directory.CreateDirectory(path);
                                    File.Copy(pendingChange.LocalOrServerItem, tmpFile, true);
                                    File.SetAttributes(tmpFile, FileAttributes.Normal);
                                }

                                using (FileStream fs = File.OpenRead(tmpFile))
                                {
                                    AddFileToZip(zipArchive, ServerFilesZipContentPrefix + serverItemRelativePath, pendingChange.CreationDate, fs);
                                }
                            }

                            if (pendingChangeBaseVersion > 0)
                            {
                                string originalFileRelativePath = originalPath == null ? serverItemRelativePath : originalPath.Substring(2).Replace('/', Path.DirectorySeparatorChar);

                                string extension = Path.GetExtension(originalFileRelativePath);
                                string baseServerItemRelativePath = originalFileRelativePath.Substring(0, originalFileRelativePath.Length - extension.Length) + ".C" + pendingChangeBaseVersion.ToString() + extension;

                                Changeset baseVersionChangeSet = versionControl.GetChangeset(pendingChangeBaseVersion, false, false);

                                pendingChange.DownloadBaseFile(tmpFile);

                                using (FileStream fs = File.OpenRead(tmpFile))
                                {
                                    AddFileToZip(zipArchive, ServerFilesZipContentPrefix + baseServerItemRelativePath, baseVersionChangeSet.CreationDate, fs);
                                }
                            }
                        }

                        pendingChangesInfoWriter.Flush();
                        pendingChangesInfo.Flush();
                        pendingChangesInfo.Position = 0;

                        DateTime referenceDate = (shelveset != null) ? shelveset.CreationDate : DateTime.Now;
                        AddFileToZip(zipArchive, "Files.txt", referenceDate, pendingChangesInfo);

                        if (itemsSource == ItemsSource.PendingChangesAndWorkfolderLocalFiles)
                        {
                            Configuration userConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                            FileInfo userConfigFileInfo = new FileInfo(userConfig.FilePath);
                            using (FileStream fs = userConfigFileInfo.OpenRead())
                            {
                                AddFileToZip(zipArchive, userConfigFileInfo.Name, userConfigFileInfo.LastWriteTime, fs);
                            }
                            
                            Expression localFilesSelector = TfZipSettings.Default.LocalFilesSelectionConfiguration;

                            foreach (string workfold in wsInfo.MappedPaths) // http://msdn.microsoft.com/en-us/library/ms181378.aspx
                            {
                                foreach (FileInfo file in new DirectoryInfo(workfold).FlattenHierarchy(delegate(DirectoryInfo dir) { try { return dir.GetDirectories(); } catch { return null; } })
                                                                                     .SelectMany(delegate(DirectoryInfo dir) { try { return dir.GetFiles(@"*"); } catch { return new FileInfo[] { }; } }))
                                {
                                    bool result = true;
                                    try
                                    {
                                        result = localFilesSelector.Evaluate(file);
                                    }
                                    catch (Exception ex) 
                                    {
                                        Console.Error.WriteLine(String.Format("An error occurred while evaluating the local file selection rules for file \"{0}\": {1}", file.FullName, ex.Message));
                                        Console.Error.WriteLine("In order to be on the safe side the file was adopted.");
                                    }
                                    if (!result)
                                    {
                                        continue;
                                    }

                                    string fileName = file.FullName;
                                    string relativeName = fileName.Substring(workfold.Length);
                                    Console.WriteLine(String.Format(@"{0}", relativeName));

                                    FileStream fs = null;
                                    try
                                    {
                                        fs = file.OpenRead();
                                    }
                                    catch (System.IO.IOException ex)
                                    {
                                        Console.Error.WriteLine(String.Format("An error occurred while opening the local file \"{0}\": {1}", fileName, ex.Message));
                                    }
                                    if (fs != null)
                                    {
                                        try
                                        {
                                            AddFileToZip(zipArchive, LocalFilesZipContentPrefix + relativeName, file.LastWriteTime, fs);
                                        }
                                        finally
                                        {
                                            fs.Dispose();
                                        }
                                    }
                                }
                            }
                        }

                        MemoryStream commentInfo = new MemoryStream();
                        StreamWriter commentInfoWriter = new StreamWriter(commentInfo);
                        commentInfoWriter.WriteLine(String.Format("Version={0}", tfZipVersion));
                        if (shelveset != null)
                        {
                            commentInfoWriter.WriteLine(String.Format("Shelveset={0}", shelveset.Name));
                            commentInfoWriter.WriteLine(String.Format("Owner={0}", shelveset.OwnerName));
                            commentInfoWriter.WriteLine(String.Format("Date={0}", shelveset.CreationDate.ToString("s")));
                            commentInfoWriter.WriteLine(String.Format("Comment={0}", shelveset.Comment));
                        }
                        else
                        {
                            commentInfoWriter.WriteLine(itemsSource.ToString());
                        }
                        commentInfoWriter.Flush();
                        commentInfo.Flush();
                        AddFileToZip(zipArchive, "Info.txt", referenceDate, commentInfo);

                        Console.WriteLine(String.Format("Zip file written to \"{0}\"", zipFilePathFull));

                        tmpFileInfo.Delete();
                    }
                }
            }

            return 0;
        }


        private static string EnsureValidZipOutFile(string zipFilePath, Func<string> fallbackFileNameStrategy)
        {
            string zipExtension = ".zip";

            if ((zipFilePath == null) || zipFilePath.EndsWith(@"\"))
            {
                string fallbackFileName = fallbackFileNameStrategy();
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fallbackFileName = fallbackFileName.Replace(new String(c, 1), "&#" + Convert.ToInt32(c).ToString() + ";");
                }

                zipFilePath = ((zipFilePath != null) ? zipFilePath : String.Empty) + fallbackFileName + zipExtension;
            }

            if (!String.Equals(Path.GetExtension(zipFilePath), zipExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                zipFilePath = zipFilePath + zipExtension;
            }

            return zipFilePath;
        }


        private static void AddFileToZip(ZipArchive zipArchive, string zipContentPath, DateTimeOffset lastWriteTime, Stream contentData)
        {
            contentData.Position = 0;

            ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry(zipContentPath);
            zipArchiveEntry.LastWriteTime = lastWriteTime;
            using (Stream s = zipArchiveEntry.Open())
            {
                contentData.CopyTo(s);
            }
        }
    }


    public static class LinqToHierarchical  // Thanks Arjan Einbu (http://blog.einbu.no/2009/07/traverse-a-hierarchy-with-linq-to-hierarchical/)
    {
        // Usage
        //foreach (FileInfo file in new DirectoryInfo(@"c:\").FlattenHierarchy(delegate(DirectoryInfo dir) { try { return dir.GetDirectories(); } catch { return null; } })
        //                                                   .SelectMany(delegate(DirectoryInfo dir) { try { return dir.GetFiles(@"*.xsd"); } catch { return new FileInfo[] {}; } }))
        //{
        //    Console.WriteLine(file.FullName);
        //}

        //foreach (DirectoryInfo directory in new DirectoryInfo(@"c:\").EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
        //{
        //    Console.WriteLine(directory.Name);
        //}

        public static IEnumerable<DirectoryInfo> EnumerateDirectories(this DirectoryInfo dir, string searchPattern, SearchOption searchOption)
        {
            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                return dir.GetDirectories(searchPattern);
            }
            else
            {
                return dir.FlattenHierarchy(delegate(DirectoryInfo dir1) { try { return dir1.GetDirectories(searchPattern); } catch { return null; } });
            }
        }

        public static IEnumerable<T> FlattenHierarchy<T>(this T node, Func<T, IEnumerable<T>> getChildsFunc)
        {
            yield return node;
            IEnumerable<T> childEnumerator = getChildsFunc(node);
            if (childEnumerator != null)
            {
                foreach (T child in childEnumerator)
                {
                    foreach (T childOrDescendant in child.FlattenHierarchy(getChildsFunc))
                    {
                        yield return childOrDescendant;
                    }
                }
            }
        }
    }
}
