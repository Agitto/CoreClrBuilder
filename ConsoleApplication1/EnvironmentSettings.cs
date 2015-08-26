﻿using System;
using System.IO;

namespace CoreClrBuilder
{
    class EnvironmentSettings
    {
        public string DNX { get; private set; }
        public string DNU { get; private set; }
        public string DNVM { get; private set; }
        public string DXVCSGet { get; private set; }
        public string UserProfile { get; private set; }
        public string WorkingDir { get; private set; }
        public string ProductConfig { get; private set; }
        public string RemoteSettingsPath { get { return string.Format(@"$/CCNetConfig/LocalProjects/{0}/BuildPortable/", BranchVersionShort); } }
        public string BranchVersion { get { return "2015.2"; } }
        public string BranchVersionShort { get { return "15.2"; } }
        public EnvironmentSettings()
        {
            DXVCSGet = "DXVCSGet.exe";
            WorkingDir = Environment.CurrentDirectory;
            UserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            DNVM = string.Format(@"{0}\.dnx\bin\dnvm.cmd", UserProfile);
            ProductConfig = Path.Combine(WorkingDir, "Product.xml");
        }
        public void InitializeDNX()
        {
            string[] paths = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User).Split(';');
            foreach (var path in paths)
            {
                if (File.Exists(Path.Combine(path, "dnx.exe")) && File.Exists(Path.Combine(path, "dnu.cmd")))
                {
                    DNX = Path.Combine(path, "dnx.exe");
                    DNU = Path.Combine(path, "dnu.cmd");
                }
            }
        }
    }
}
