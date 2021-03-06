﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using NetIde.Shell.Interop;

namespace NetIde.Update
{
    public abstract class PackageManager
    {
        public const string CorePackageId = "NetIde.Package.Core";
        public const string RuntimePackageId = "NetIde.Runtime";

        public static bool IsValidPackageId(ContextName context, string packageId)
        {
            return
                packageId.StartsWith("NetIde.Package.", StringComparison.OrdinalIgnoreCase) ||
                packageId.StartsWith(context.Name + ".Package.", StringComparison.OrdinalIgnoreCase);
        }

        public ContextName Context { get; private set; }

        protected PackageManager(ContextName context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            Context = context;

            if (Context.Name.IndexOfAny(new[] { '/', '\\' }) != -1)
                throw new PackageInstallationException(Labels.ContextNameInvalid, 1);
        }

        public abstract void Execute();

        protected bool TryParseEntryPoint(string entryPoint, out string entryAssembly, out string entryTypeName)
        {
            entryAssembly = null;
            entryTypeName = null;

            int pos = entryPoint.IndexOf(',');

            if (pos == -1)
                return false;

            entryTypeName = entryPoint.Substring(0, pos).Trim();
            entryAssembly = entryPoint.Substring(pos + 1).Trim();

            return true;
        }

        protected IDisposable CreateAppDomain(string packagePath, string entryPoint, out INiPackage package)
        {
            string entryAssembly;
            string entryTypeName;

            if (!TryParseEntryPoint(entryPoint, out entryAssembly, out entryTypeName))
                throw new PackageInstallationException(Labels.InvalidManifest, 3);

            var setup = new AppDomainSetup
            {
                ApplicationBase = packagePath,
                ApplicationName = "Net IDE package manager"
            };

            var appDomain = AppDomain.CreateDomain(
                setup.ApplicationName,
                AppDomain.CurrentDomain.Evidence,
                setup
            );

            try
            {
                package = (INiPackage)appDomain.CreateInstanceAndUnwrap(
                    entryAssembly,
                    entryTypeName
                );

                return new AppDomainUnloader(appDomain);
            }
            catch
            {
                AppDomain.Unload(appDomain);

                throw;
            }
        }

        protected void ExtractPackage(string packageFileName, string target)
        {
            using (var zipFile = new ZipFile(packageFileName))
            {
                const string toolsPrefix = "Tools/";

                foreach (ZipEntry entry in zipFile)
                {
                    string fileName = entry.Name;

                    if (fileName.StartsWith(toolsPrefix, StringComparison.OrdinalIgnoreCase))
                        fileName = fileName.Substring(toolsPrefix.Length);
                    else if (!fileName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                        continue;

                    fileName = Path.Combine(target, fileName.Replace('/', Path.DirectorySeparatorChar));

                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));

                    using (var source = zipFile.GetInputStream(entry))
                    using (var destination = File.Create(fileName))
                    {
                        source.CopyTo(destination);
                    }
                }
            }
        }

        protected string GetFileSystemRoot()
        {
            using (var contextKey = OpenContextRegistry(false))
            {
                if (contextKey != null)
                    return contextKey.GetValue("InstallationPath") as string;
                else
                    throw new PackageInstallationException(Labels.ContextDoesNotExist, 2);
            }
        }

        protected RegistryKey OpenContextRegistry(bool writable)
        {
            return PackageRegistry.OpenRegistryRoot(writable, Context);
        }

        public static string GetInstalledVersion(ContextName context, string packageId)
        {
            using (var contextKey = PackageRegistry.OpenRegistryRoot(false, context))
            {
                if (contextKey != null)
                {
                    using (var packageKey = contextKey.OpenSubKey("InstalledProducts\\" + packageId))
                    {
                        // This method is used to resolve dependencies. If we don't
                        // have the package installed, we check whether it's a valid
                        // package ID at all. If not, the dependency probably is an
                        // invalid dependency (or the NetIde.Runtime dependency)
                        // and we completely ignore it.

                        if (packageKey != null)
                            return (string)packageKey.GetValue("Version");
                    }
                }
            }

            return null;
        }

        public static string GetEntryAssemblyLocation(string installationPath)
        {
            if (installationPath == null)
                throw new ArgumentNullException("installationPath");

            return Path.Combine(installationPath, "bin", "NetIde.exe");
        }

        public static bool IsCorePackage(string packageId)
        {
            if (packageId == null)
                throw new ArgumentNullException("packageId");

            return packageId.EndsWith(".Package.Core", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCorePackage(string packageId, ContextName context)
        {
            if (packageId == null)
                throw new ArgumentNullException("packageId");
            if (context == null)
                throw new ArgumentNullException("context");

            return String.Equals(packageId, context.Name + ".Package.Core", StringComparison.OrdinalIgnoreCase);
        }

        private class AppDomainUnloader : IDisposable
        {
            private AppDomain _appDomain;
            private bool _disposed;

            public AppDomainUnloader(AppDomain appDomain)
            {
                _appDomain = appDomain;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    if (_appDomain != null)
                    {
                        AppDomain.Unload(_appDomain);
                        _appDomain = null;
                    }

                    _disposed = true;
                }
            }
        }
    }
}
