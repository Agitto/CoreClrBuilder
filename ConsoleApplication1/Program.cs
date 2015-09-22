﻿using System;
using System.IO;

namespace CoreClrBuilder
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 1 && (args[0] == "-h" || args[0] == "-help"))
            {
                Console.Write(@"
CoreClrBuilder.exe [-config:<name>] [env_init] [remove] [get] [build] [test]
    env_init [-u] [-r <runtime>] [-arch <name>] [-v <version>]
    build [dnx451] [dotnet] [dnxcore50] 

-config:<config_name> - configuration file with projects and project settings

env_init - dnx and dnvm installation, getting nuget.config, product.xml
    -u - use unstable version of dnx
    -r - runtime clr or coreclr
    -arch - x64 or x86
    -v - version of dnx (Example: 1.0.0-beta4-11566)

remove - remove direcories with projects

get - get projects from DXVCS    

build - restore packages and build projects
    dnx451 or dotnet or dnxcore50 - target framework

test - run tests
");
                return 0;
            }
            StepSettings stepSettings = new StepSettings(args);
            DNXSettings dnxSettings = new DNXSettings(args);
            EnvironmentSettings envSettings = new EnvironmentSettings(args);
            if (!File.Exists(envSettings.ProductConfig) && string.IsNullOrEmpty(envSettings.BranchVersion))
            {
                Console.Write("Please specify branch version (Example: -branch 15.2) or put Product.xml near CoreClrBuilder.exe");
                return 0;
            }
            Executor executor = new Executor();
            return executor.ExecuteTasks(dnxSettings, stepSettings, envSettings);
        }
    }

}
