﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Xsl;

namespace CoreClrBuilder
{
    class RestoreCommand : Command
    {
        public RestoreCommand(EnvironmentSettings settings, CoreClrProject project) :
            base(settings.DNU, string.Format("restore {0}", project.LocalPath), "call dnu restore", settings.WorkingDir) { }
    }
    class BuildCommand : Command {

        public BuildCommand(EnvironmentSettings settings, CoreClrProject project) {
            string buildParams = string.Format("pack {0} --configuration {1}", project.LocalPath, project.BuildConfiguration);
            if (!string.IsNullOrEmpty(project.BuildFramework))
                buildParams += string.Format(" --framework {0}", project.BuildFramework);
            Init(settings.DNU, buildParams, "build", settings.WorkingDir);
        }
    }
    class InstallPackageCommand : Command
    {
        public InstallPackageCommand(EnvironmentSettings settings, CoreClrProject project)
        {
            string args = PlatformPathsCorrector.Inst.Correct(string.Format(@"packages add {0}\bin\{1}\{2} {3}\.dnx\packages", project.LocalPath, project.BuildConfiguration, project.NugetPackageName, settings.UserProfile), Platform.Windows);
            Init(settings.DNU, args, "install package", settings.WorkingDir);
        }
    }
    class RunTestsCommand : Command
    {
        protected override bool ThrowWrongExitCodeException { get { return false; } }
        public RunTestsCommand(EnvironmentSettings settings, CoreClrProject project) :
            base(settings.DNX, string.Format(@"-p {0} --configuration {1} test -xml {2}", project.LocalPath, project.BuildConfiguration, project.TestResultFileName), "run tests", settings.WorkingDir)
        { }
    }
    class GetFromVCSCommand : Command
    {
        public GetFromVCSCommand(EnvironmentSettings settings, string remotePath, string workingDir) :
            this(settings, remotePath, string.Empty, string.Empty, workingDir)
        { }
        public GetFromVCSCommand(EnvironmentSettings settings, string remotePath, string localPath, string comment, string workingDir)
        {
            
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentNullException("remote path cannot be null");
            //if (settings.Platform == Platform.Windows)
                Init(settings.DXVCSGet, string.Format("vcsservice.devexpress.devx {0} {1}", remotePath, localPath), comment, workingDir);
            //else
                //Init(settings.DXVCSGet, string.Format("vcsget.py {0} {1}", remotePath, localPath), comment, workingDir);
        }
    }
    class GetProductConfigCommand : GetFromVCSCommand
    {
        string productConfig;
        public GetProductConfigCommand(EnvironmentSettings settings) :
            base(settings, string.Format("$/CCNetConfig/LocalProjects/{0}/BuildPortable/Product.xml", settings.BranchVersionShort), string.Empty, "Get Product.xml", settings.WorkingDir)
        {
            productConfig = settings.ProductConfig;
        }

        public override void Execute()
        {
            if (!File.Exists(productConfig))
                base.Execute();
        }
    }
    class DownloadDNVMCommand : Command
    {
        string dnvm;
        public DownloadDNVMCommand(EnvironmentSettings settings) 
            
        {
            dnvm = settings.DNVM;
            string executableFile = "powershell.exe";
            string args = "-NoProfile -ExecutionPolicy unrestricted -Command \" &{$Branch = 'dev'; iex((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}\"";
            Init(executableFile, args, "Download dnvm", settings.WorkingDir);
        }

        public override void Execute()
        {
            if (!File.Exists(dnvm))
                base.Execute();
        }
    }
    class InstallDNXCommand : Command {
        public InstallDNXCommand(EnvironmentSettings settings, DNXSettings dnxsettings) 
        {
            if (settings.Platform == Platform.Windows)
            {
                Init(settings.DNVM, dnxsettings.CreateArgsForDNX(), "Install dnx", settings.WorkingDir);
            }
            else {
                Init("bash", "dnxInstall.sh", "Install dnx", settings.WorkingDir);

            }
        }
    }
    class GetNugetConfigCommand : GetFromVCSCommand
    {
        string workingDir;
        public GetNugetConfigCommand(EnvironmentSettings settings, DNXSettings dnxsettings) :
            base(
                settings,
                string.Format("$/{0}/Win/NuGet.Config", settings.BranchVersion),
                PlatformPathsCorrector.Inst.Correct(@"Win\", Platform.Windows),
                "get nuget.config",
                settings.WorkingDir)
        {
            workingDir = settings.WorkingDir;
        }
        public override void Execute()
        {
            if (!File.Exists(Path.Combine(workingDir, PlatformPathsCorrector.Inst.Correct(@"Win\NuGet.Config", Platform.Windows))))
                base.Execute();
        }
    }
    class GetInstallDNXScriptComamnd : GetFromVCSCommand
    {
        string workingDir;
        public GetInstallDNXScriptComamnd(EnvironmentSettings settings, DNXSettings dnxsettings) :
            base(
                settings,
                string.Format("$/CCNetConfig/LocalProjects/{0}/BuildPortable/dnxInstall.sh", settings.BranchVersionShort), 
                @"./",
                "get installation script",
                settings.WorkingDir)
        {
            workingDir = settings.WorkingDir;
        }
    }
    class CopyProjectsCommand : ICommand
    {
        ProductInfo productInfo;
        string copyPath;
        bool copySubDirs;
        public CopyProjectsCommand(ProductInfo productInfo, string copyPath, bool copySubDirs) {
            this.productInfo = productInfo;
            this.copyPath = copyPath;
            this.copySubDirs = copySubDirs;
        }

        void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                Console.WriteLine(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
                return;
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        public void Execute()
        {
            foreach (var item in productInfo.Projects)
            {
                if (Directory.Exists(item.LocalPath))
                {
                    Console.WriteLine("Begin copy dir {0} ", item.LocalPath);
                    DirectoryCopy(item.LocalPath, Path.Combine(copyPath, item.LocalPath), true);
                }
                else
                {
                    Console.WriteLine("dir {0} doesn't exist", item.LocalPath);
                }
            }
        }
    }
    class UnixGrantAccessCommand : Command
    {
        public UnixGrantAccessCommand(string path, string workingDir) : base("chmod", "-R 777 " + path, "grant access to folder " + path, workingDir)
        {
        }
    }
    class CollectArtifactsCommand : ICommand
    {
        ProductInfo info;
        string destFolder;
        public CollectArtifactsCommand(ProductInfo info, string destFolder, string buildFramework)
        {
            this.info = info;
            if (!string.IsNullOrEmpty(buildFramework))
                this.destFolder = string.Format(@"{0}\{1}", destFolder, buildFramework);
            else
                this.destFolder = destFolder;
        }
        public void Execute()
        {
            if (Directory.Exists(destFolder))
                Directory.Delete(destFolder, true);
            Directory.CreateDirectory(destFolder);

            foreach (var project in info.Projects)
            {
                string localPackagePath = PlatformPathsCorrector.Inst.Correct(string.Format(@"{0}\bin\{1}\{2}", project.LocalPath, project.BuildConfiguration, project.NugetPackageName), Platform.Windows);
                if (File.Exists(localPackagePath))
                {
                    Console.WriteLine("Start copy package {0}", project.NugetPackageName);
                    File.Copy(localPackagePath, destFolder + "\\" + project.NugetPackageName);
                }
                else
                    Console.WriteLine("Package {0} doesn't exist", project.NugetPackageName);
            }
        }
    }
    class CommandFactory
    {
        EnvironmentSettings envSettings;
        
        ProductInfo productInfo;

        public CommandFactory(EnvironmentSettings settings, ProductInfo productInfo)
        {
            this.envSettings = settings;
            this.productInfo = productInfo;
        }
        public ICommand InstallEnvironment(DNXSettings dnxsettings)
        {
            if (envSettings.Platform == Platform.Windows)
                return new BatchCommand(
                    new DownloadDNVMCommand(envSettings),
                    new InstallDNXCommand(envSettings, dnxsettings),
                    new GetNugetConfigCommand(envSettings, dnxsettings));
            else
                return new BatchCommand(
                    new GetInstallDNXScriptComamnd(envSettings, dnxsettings),
                    new InstallDNXCommand(envSettings, dnxsettings),
                    new GetNugetConfigCommand(envSettings, dnxsettings));
        }
        public ICommand GetProjectsFromVCS()
        {
            BatchCommand batchCommand = new BatchCommand();
            foreach (var project in productInfo.Projects)
                batchCommand.Add(new GetFromVCSCommand(envSettings, project.VSSPath, project.LocalPath, string.Format("get {0} from VCS", project.ProjectName), envSettings.WorkingDir ));
            return batchCommand;
        }
        public ICommand BuildProjects()
        {
            BatchCommand batchCommand = new BatchCommand();
            foreach (var project in productInfo.Projects)
            {
                if (envSettings.Platform == Platform.Unix)
                    batchCommand.Add(new UnixGrantAccessCommand(project.LocalPath, envSettings.WorkingDir));
                batchCommand.Add(new RestoreCommand(envSettings, project));
                batchCommand.Add(new BuildCommand(envSettings, project));
                batchCommand.Add(new InstallPackageCommand(envSettings, project));
            }
            return batchCommand;
        }
        public ICommand RunTests()
        {
            BatchCommand batchCommand = new BatchCommand();
            if (envSettings.Platform == Platform.Windows)
                batchCommand.Add(new GetFromVCSCommand(envSettings, string.Format("$/CCNetConfig/LocalProjects/{0}/BuildPortable/NUnitXml.xslt", envSettings.BranchVersionShort), envSettings.WorkingDir));
            batchCommand.Add(new ActionCommand("Tests clear", () =>
            {
                foreach (var project in productInfo.Projects)
                {
                    string xUnitResults = Path.Combine(envSettings.WorkingDir, project.TestResultFileName);
                    string nUnitResults = Path.Combine(envSettings.WorkingDir, project.NunitTestResultFileName);

                    if (File.Exists(xUnitResults))
                        File.Delete(xUnitResults);
                }
            }));
            
            foreach (var project in productInfo.Projects)
                batchCommand.Add(new RunTestsCommand(envSettings, project));

            batchCommand.Add(new ActionCommand("Tests transform", () =>
            {
                XslCompiledTransform xslt = new XslCompiledTransform();
                xslt.Load("NUnitXml.xslt");
                List<string> nUnitTestFiles = new List<string>();
                foreach (var project in productInfo.Projects)
                {
                    string xUnitResults = Path.Combine(envSettings.WorkingDir, project.TestResultFileName);
                    string nUnitResults = Path.Combine(envSettings.WorkingDir, project.NunitTestResultFileName);

                    if (File.Exists(nUnitResults))
                        File.Delete(nUnitResults);
                    if (File.Exists(xUnitResults))
                    {
                        xslt.Transform(xUnitResults, nUnitResults);
                        nUnitTestFiles.Add(nUnitResults);
                    }
                }
                NUnitMerger.MergeFiles(nUnitTestFiles, "nunit-result.xml");
            }));
            return batchCommand;
        }
        public ICommand CopyProjects(string copyPath, bool copySubDirs) {
            return new CopyProjectsCommand(productInfo, copyPath, copySubDirs);
        }
        public ICommand RemoveProjects()
        {
            return new RemoveProjectsCommand(productInfo);
        }
        internal ICommand CollectArtifacts(string destFolder, string buildFramework)
        {
            return new CollectArtifactsCommand(productInfo, destFolder, buildFramework);
        }
    }
}
