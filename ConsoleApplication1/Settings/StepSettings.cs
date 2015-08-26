﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreClrBuilder
{
    class StepSettings
    {
        public bool Build { get; private set; }
        public bool RunTests { get; private set; }
        public bool RestorePackages { get; private set; }
        public bool GetProjectsFromDXVCS { get; private set; }

        public StepSettings(string [] args)
        {
            Build = true;
            RunTests = true;
            RestorePackages = true;
            GetProjectsFromDXVCS = true;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Compare(args[i], "exclude_steps:", true) == 0 && i < args.Length - 1)
                {
                    while (i + 1 < args.Length)
                    {
                        if (string.Compare(args[i + 1], "get", true) == 0)
                            GetProjectsFromDXVCS = false;
                        else if (string.Compare(args[i + 1], "restore", true) == 0)
                            RestorePackages = false;
                        else if (string.Compare(args[i + 1], "build", true) == 0)
                            Build = false;
                        else if (string.Compare(args[i + 1], "test", true) == 0)
                            RunTests = false;
                        else
                            break;
                        i++;
                    }
                }
            }
        }
    }
}
