
using System.Collections.Generic;

namespace StatLight.Core.WebServer.XapInspection
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml.Linq;
    using Ionic.Zip;
    using StatLight.Core.Common;

    public class XapReader
    {
        private readonly ILogger _logger;

        public XapReader(ILogger logger)
        {
            _logger = logger;
        }

        public TestFileCollection LoadXapUnderTest(string archiveFileName)
        {
            var files = new List<ITestFile>();
            string testAssemblyFullName = null;

            // manually read in the file because the file lock used in ZipFile seems to fail on reading...
            var fileStream = File.ReadAllBytes(archiveFileName);

            using (var archive = ZipFile.Read(fileStream))
            {
                var appManifest = LoadAppManifest(archive);

                if (appManifest != null)
                {
                    string testAssemblyName = GetTestAssemblyNameFromAppManifest(appManifest);

                    AssemblyName assemblyName = GetAssemblyName(archive, testAssemblyName);
                    if (assemblyName != null)
                    {
                        testAssemblyFullName = assemblyName.ToString();
                    }
                }

                files.AddRange((from zipEntry in archive
                                let fileBytes = ReadFileIntoBytes(archive, zipEntry.FileName)
                                select new TestFile(zipEntry.FileName, fileBytes)).ToList());

                foreach (var item in files)
                    _logger.Debug("XapItems.FilesContainedWithinXap = {0}".FormatWith(item.FileName));
            }

            var xapItems = new TestFileCollection(_logger, testAssemblyFullName, files);


            return xapItems;
        }

        private static AssemblyName GetAssemblyName(ZipFile zip1, string testAssemblyName)
        {
            string tempFileName = Path.GetTempFileName();
            var fileData = ReadFileIntoBytes(zip1, testAssemblyName);
            if (fileData != null)
            {
                File.WriteAllBytes(tempFileName, fileData);
                return AssemblyName.GetAssemblyName(tempFileName);
            }
            return null;
        }

        private static string GetTestAssemblyNameFromAppManifest(string appManifest)
        {
            var root = XElement.Parse(appManifest);

            var entryPointAssemblyNode = root.Attribute("EntryPointAssembly");

            if (entryPointAssemblyNode == null)
                throw new StatLightException("Cannot find the EntryPointAssembly attribute in the AppManifest.xaml");

            return entryPointAssemblyNode.Value + ".dll";
        }

        private static string LoadAppManifest(ZipFile zip1)
        {
            var fileData = ReadFileIntoBytes(zip1, "AppManifest.xaml");
            if (fileData != null)
            {
                string xaml = Encoding.UTF8.GetString(fileData);
                if (xaml[0] == '<')
                    return xaml;
                return xaml.Substring(1);
            }

            return null;
        }

        private static byte[] ReadFileIntoBytes(ZipFile zipFile, string fileName)
        {
            var file = zipFile[fileName];
            if (file == null)
                return null;

            using (var stream = new MemoryStream())
            {
                file.Extract(stream);
                return stream.ToArray();
            }
        }

        public static string GetRuntimeVersion(string xapPath)
        {
            using (var archive = ZipFile.Read(xapPath))
            {
                ZipEntry appManifestEntry = archive["AppManifest.xaml"];
                if (appManifestEntry == null) 
                    return null; 

                var xAppManifest = XElement.Load(appManifestEntry.OpenReader());

                var runtimeVersion = xAppManifest.Attribute("RuntimeVersion");
                return runtimeVersion != null ? runtimeVersion.Value : null;
            }
        }
    }
}
