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

namespace TfZip
{
    class Program
    {
        static void Usage()
        {
            System.Console.WriteLine(String.Format("Usage:   TfZip -shelveset <shelvesetname>[;<selvesetowner>] [<zipoutfile>]      Creates a zipfile containing the files in the shelveset and their previous versions."));
            System.Console.WriteLine(String.Format("                                                                                A filename will be generated from <shelvesetname> if <zipoutfile> ends with '\\' or is missing."));
            System.Console.WriteLine(String.Format("         TfZip -pending <zipoutfile>                                            Creates a zipfile containing all pending files and their previous versions."));
        }


        enum ItemsSource
        {
            ShelveSet,
            Pending,
        }


        static int Main(String[] args)
        {
            //System.Diagnostics.Debugger.Launch();

            String zipFilePath = null;

            ItemsSource itemsSource;
            String shelveSetSpec = null;
            String shelvesetName = null;
            String shelvesetOwner = null;

            if (args.Length == 0)
            {
                Usage();
                return 3;
            }

            WorkspaceInfo wsInfo = null;

            if (String.Equals(args[0], "-shelveset", StringComparison.InvariantCultureIgnoreCase))
            {
                if ((args.Length < 2) || (args.Length > 3))
                {
                    Usage();
                    return 3;
                }

                shelveSetSpec = args[1];
                WorkspaceSpec.Parse(shelveSetSpec, RepositoryConstants.AuthenticatedUser, out shelvesetName, out shelvesetOwner);
                if (String.IsNullOrEmpty(shelvesetName) || String.IsNullOrEmpty(shelvesetOwner))
                {
                    Usage();
                    return 3;
                }

                if (args.Length == 3)
                {
                    zipFilePath = args[2];
                }

                zipFilePath = EnsureValidZipOutFile(zipFilePath, delegate() { return shelvesetName; });

                itemsSource = ItemsSource.ShelveSet;
            }
            else if (String.Equals(args[0], "-pending", StringComparison.InvariantCultureIgnoreCase))
            {
                if ((args.Length < 1) || (args.Length > 2))
                {
                    Usage();
                    return 3;
                }

                if (args.Length == 2)
                {
                    zipFilePath = args[1];
                }

                try
                {
                    zipFilePath = EnsureValidZipOutFile(zipFilePath, delegate()
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

                itemsSource = ItemsSource.Pending;
            }
            else
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

            using (TfsTeamProjectCollection tfsServer = new TfsTeamProjectCollection(wsInfo.ServerUri, new UICredentialsProvider()))
            {
                tfsServer.EnsureAuthenticated();

                VersionControlServer versionControl = (VersionControlServer)tfsServer.GetService(typeof(VersionControlServer));
                if (versionControl == null)
                {
                    System.Console.WriteLine(String.Format("Could not access VersionControlServer service."));
                    return 1;
                }

                Shelveset shelveset = null;
                PendingChange[] pendingChanges;
                if (itemsSource == ItemsSource.Pending)
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
                        System.Console.WriteLine(String.Format("Could not find shelveset \"{0}\".", shelveSetSpec));
                        return 1;
                    }
                    if ((pendingSets == null) || (pendingSets.Length == 0) || (pendingSets.Length > 1))
                    {
                        throw new InvalidOperationException("Unexpected QueryShelvedChanges result.");
                    }

                    pendingChanges = pendingSets[0].PendingChanges;

                    ShelvesetUri shelvesetUri = new ShelvesetUri(shelvesetName, shelvesetOwner, UriType.Normal);
                    shelveset = versionControl.ArtifactProvider.GetShelveset(shelvesetUri.Uri);
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
                        System.Console.WriteLine(String.Format("Cannot write to outfile \"{0}\".", zipFilePath));
                        return 1;
                    }
                }
                else
                {
                    if (!zipFilePathInfo.Directory.Exists)
                    {
                        System.Console.WriteLine(String.Format("Cannot access outfile path \"{0}\".", zipFilePath));
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
                            System.Console.WriteLine(String.Format("Cannot use temporary storage file \"{0}\"." + Environment.NewLine + "{1}", tmpFileInfo, ex.Message));
                        }

                        MemoryStream pendingChangesInfo = new MemoryStream();
                        StreamWriter pendingChangesInfoWriter = new StreamWriter(pendingChangesInfo);

                        foreach (PendingChange pendingChange in pendingChanges)
                        {
                            string changeType = PendingChange.GetLocalizedStringForChangeType(pendingChange.ChangeType);
                            string basedOn = pendingChange.IsAdd ? String.Empty : String.Format("C{0}", pendingChange.Version);
                            string originalPath = pendingChange.IsRename ? pendingChange.SourceServerItem : null;
                            string originalPathInfo = originalPath != null ? String.Format("{0}", originalPath) : String.Empty;
                            string info = String.Format("{0}|{1}|{2}|{3}", pendingChange.ServerItem, changeType, basedOn, originalPathInfo);
                            System.Console.WriteLine(info);
                            pendingChangesInfoWriter.WriteLine(info);

                            if (pendingChange.ItemType != ItemType.File)
                            {
                                continue;
                            }

                            string serverItemRelativePath = pendingChange.ServerItem.Substring(2).Replace('/', Path.DirectorySeparatorChar);

                            if (pendingChange.LocalOrServerItem.StartsWith("$"))
                            {
                                pendingChange.DownloadShelvedFile(tmpFile);
                            }
                            else
                            {
                                string path = Path.GetDirectoryName(tmpFile);
                                Directory.CreateDirectory(path);
                                File.Copy(pendingChange.LocalOrServerItem, tmpFile, true);
                            }

                            using (FileStream fs = File.OpenRead(tmpFile))
                            {
                                AddFileToZip(zipArchive, serverItemRelativePath, pendingChange.CreationDate, fs);
                            }

                            if (pendingChange.IsEdit && !pendingChange.IsAdd)
                            {
                                string originalFileRelativePath = originalPath == null ? serverItemRelativePath : originalPath.Substring(2).Replace('/', Path.DirectorySeparatorChar);

                                string extension = Path.GetExtension(originalFileRelativePath);
                                int pendingChangeBaseVersion = pendingChange.Version;
                                string baseServerItemRelativePath = originalFileRelativePath.Substring(0, originalFileRelativePath.Length - extension.Length) + ".C" + pendingChangeBaseVersion.ToString() + extension;

                                Changeset baseVersionChangeSet = versionControl.GetChangeset(pendingChangeBaseVersion, false, false);

                                pendingChange.DownloadBaseFile(tmpFile);

                                using (FileStream fs = File.OpenRead(tmpFile))
                                {
                                    AddFileToZip(zipArchive, baseServerItemRelativePath, baseVersionChangeSet.CreationDate, fs);
                                }
                            }
                        }

                        pendingChangesInfoWriter.Flush();
                        pendingChangesInfo.Flush();
                        pendingChangesInfo.Position = 0;

                        AddFileToZip(zipArchive, "Files.txt", (shelveset != null) ? shelveset.CreationDate : DateTime.Now, pendingChangesInfo);

                        if (shelveset != null)
                        {
                            MemoryStream commentInfo = new MemoryStream();
                            StreamWriter commentInfoWriter = new StreamWriter(commentInfo);
                            commentInfoWriter.WriteLine(String.Format("Shelveset={0}", shelveset.Name));
                            commentInfoWriter.WriteLine(String.Format("Owner={0}", shelveset.OwnerName));
                            commentInfoWriter.WriteLine(String.Format("Date={0}", shelveset.CreationDate.ToString("s")));
                            commentInfoWriter.WriteLine(String.Format("Comment={0}", shelveset.Comment));
                            commentInfoWriter.Flush();
                            commentInfo.Flush();
                            AddFileToZip(zipArchive, "Info.txt", shelveset.CreationDate, commentInfo);
                        }

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
}
