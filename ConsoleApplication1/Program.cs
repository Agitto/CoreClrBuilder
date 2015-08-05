﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace CoreClrBuilder
{
    public class WrongExitCodeException : Exception
    {
        public WrongExitCodeException(string fileName, string arguments, int exitCode, List<string> output)
            :
            base(String.Format("Process \"{0}\" has finished with error code {1}\nArguments :{2}\nOutput :\n{3}", fileName, exitCode, arguments, String.Join("\n", output.ToArray())))
        {
        }
    }
    class Program
    {
        static StreamReader errorReader;
        static List<string> outputErrors = new List<string>();
        static int Main(string[] args)
        {
            Executor executor = new Executor();
            return executor.ExecuteTasks();
        }
    }

    class Executor
    {
        static StreamReader /*outputReader,*/ errorReader;
        static List<string> outputErrors = new List<string>();
        static string WorkingDir;
        XmlTextWriter tmpXml;
        StringBuilder taskBreakingLog = new StringBuilder();
        public int ExecuteTasks()
        {
            tmpXml = new XmlTextWriter(new StringWriter(taskBreakingLog));
            tmpXml.Formatting = Formatting.Indented;
            int result = 0;
            try
            {
                WorkingDir = Environment.CurrentDirectory;

                List<string> testResults = new List<string>();
                string productConfig = Path.Combine(WorkingDir, "Product.xml");
                if (!File.Exists(productConfig))
                    result += DoWork("DXVCSGet.exe", "vcsservice.devexpress.devx $/CCNetConfig/LocalProjects/15.2/BuildPortable/Product.xml");

                ProductInfo productInfo = new ProductInfo(productConfig);
                result += DoWork("DXVCSGet.exe", "vcsservice.devexpress.devx $/CCNetConfig/LocalProjects/15.2/BuildPortable/build.bat");
                result += DoWork("build.bat", null);
                if (result == 0)
                {
                    result += DoWork("DXVCSGet.exe", "vcsservice.devexpress.devx $/CCNetConfig/LocalProjects/15.2/BuildPortable/NUnitXml.xslt");
                    XslCompiledTransform xslt = new XslCompiledTransform();
                    xslt.Load("NUnitXml.xslt");

                    foreach (var project in productInfo.Projects)
                    {
                        int testResult = DoWork("dnx", string.Format(@"{0} test -xml {1}", project.LocalPath, project.TestResultFileName));
                        result += testResult;
                        if (testResult == 0)
                        {
                            string xUnitResults = Path.Combine(WorkingDir, project.TestResultFileName);
                            string nUnitResults = Path.Combine(WorkingDir, project.NunitTestResultFileName);
                            if (File.Exists(nUnitResults))
                                File.Delete(nUnitResults);
                            if (File.Exists(xUnitResults))
                                xslt.Transform(xUnitResults, nUnitResults);
                        }
                    }
                }
            }
            catch (Exception)
            {
                result = 1;
            }
            tmpXml.Close();
            if (taskBreakingLog.Length > 0)
            {
                taskBreakingLog.Insert(0, "<vssPathsByTasks>\r\n");
                taskBreakingLog.Append("\r\n</vssPathsByTasks>\r\n");
                string currLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                File.WriteAllText(Path.Combine(currLocation, "vssPathsByTasks.xml"), taskBreakingLog.ToString());
            }
            return result > 0 ? 1 : 0;
        }
        static void Execute(string fileName, string args)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            //startInfo = new ProcessStartInfo(fileName, args);
            startInfo = new ProcessStartInfo(fileName, args);
            startInfo.WorkingDirectory = WorkingDir;
            startInfo.UseShellExecute = false;
            //startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = false;

            process.StartInfo = startInfo;
            process.Start();

            //outputReader = process.StandardOutput;
            //Thread outputThread = new Thread(new ThreadStart(StreamReaderThread_Output));
            //outputThread.Start();

            errorReader = process.StandardError;

            for (;;)
            {
                string strLogContents = errorReader.ReadLine();
                if (strLogContents == null)
                    break;
                else
                    outputErrors.Add(strLogContents);
            }
            //if (outputErrors.Count > 0)
            //{
            //    //using (FileStream fileStream = new FileStream(Path.Combine(WorkingDir, "errorLog.txt"), FileMode.Append, FileAccess.Write)) {
            //    //    using (StreamWriter writer = new StreamWriter(fileStream)) {
            //    Console.WriteLine("-[Error]-----------------------------------------------------------------");
            //    foreach (var error in outputErrors)
            //        Console.WriteLine(error);
            //    Console.WriteLine("-------------------------------------------------------------------------");
            //}
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new WrongExitCodeException(process.StartInfo.FileName, process.StartInfo.Arguments, process.ExitCode, outputErrors);
            outputErrors.Clear();
            errorReader = null;
            process = null;
        }
        int DoWork(string fileName, string args)
        {
            //string cclistner = Environment.GetEnvironmentVariable("CCNetListenerFile", EnvironmentVariableTarget.Process);
            //string cclistner = "output.xml";
            Stopwatch timer = new Stopwatch();
            timer.Start();
            try
            {
                Execute(fileName, args);
                OutputLog.LogTextNewLine("\r\n<<<<done. Elapsed time {0:F2} sec",  timer.Elapsed.TotalSeconds);
                return 0;
            }
            catch (Exception e)
            {
                OutputLog.LogTextNewLine("\r\n<<<<exception. Elapsed time {0:F2} sec", timer.Elapsed.TotalSeconds);
                OutputLog.LogException(e);
                lock (tmpXml)
                {
                    tmpXml.WriteStartElement("task");
                    tmpXml.WriteStartElement("error");
                    tmpXml.WriteElementString("message", e.ToString());
                    tmpXml.WriteEndElement();
                    tmpXml.WriteEndElement();
                }
                return 1;
            }
        }
        static void WriteCCFile(string cclistner, Task task, int pos, int count)
        {
            StringBuilder res = new StringBuilder();
            using (XmlTextWriter xtw = new XmlTextWriter(new StringWriter(res)))
            {
                xtw.WriteStartElement("data");
                xtw.WriteStartElement("Item");
                xtw.WriteAttributeString("Time", DateTime.Now.ToString());
                xtw.WriteAttributeString("Data", String.Format("{2} ({0}/{1})", pos + 1, count, task.GetType().ToString()));
                xtw.WriteEndElement();
                PropertyInfo[] propertyInfos = task.GetType().GetProperties();
                foreach (PropertyInfo propertyInfo in propertyInfos)
                {
                    string propertyValue = FormatPropertyValue(task, propertyInfo);
                    if (!String.IsNullOrEmpty(propertyValue))
                    {
                        xtw.WriteStartElement("Item");
                        xtw.WriteAttributeString("Time", String.Empty);
                        if (propertyValue.Length > 80)
                            propertyValue = propertyValue.Substring(0, 40) + "..." + propertyValue.Substring(propertyValue.Length - 40);
                        xtw.WriteAttributeString("Data", string.Format("    {0}='{1}'", propertyInfo.Name, propertyValue));
                        xtw.WriteEndElement();
                    }
                }
                xtw.WriteEndElement();
            }
            try
            {
                File.WriteAllText(cclistner, res.ToString(), Encoding.Unicode);
            }
            catch
            {
            }
        }
        static string FormatPropertyValue(Task task, PropertyInfo propertyInfo)
        {
            try
            {
                string propertyValue = string.Empty;
                object value = propertyInfo.GetValue(task, null);
                if (value is IEnumerable && !(value is string))
                {
                    foreach (object elem in (IEnumerable)value)
                        propertyValue = string.Format("{0}{1}'{2}'",
                            propertyValue, propertyValue.Length > 0 ? ", " : string.Empty, elem.ToString());
                }
                else
                {
                    if (value != null)
                        propertyValue = value.ToString();
                }
                return propertyValue;
            }
            catch
            {
                return String.Empty;
            }
        }
    }

    class ProductInfo {

        List<CoreClrProject> projects = new List<CoreClrProject>();
        public List<CoreClrProject> Projects { get { return projects; } }

        public ProductInfo(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            string releaseVersion = doc.SelectSingleNode("/ProductInfo/ProductInformation/Version").InnerText;
            XmlNode vssLocations = doc.SelectSingleNode("/ProductInfo/VSSLocations");
            foreach (XmlNode location in vssLocations.ChildNodes)
            {
                string buildConf = location.Attributes["BuildConfiguration"] == null ? "Debug" : location.Attributes["BuildConfiguration"].InnerText;
                projects.Add(new CoreClrProject(location.Attributes["VSSPath"].InnerText, location.Attributes["ReferenceName"].InnerText, releaseVersion, buildConf));
            }
        }
    }

    class CoreClrProject {
        public bool IsValid { get { return !string.IsNullOrEmpty(VSSPath) && !string.IsNullOrEmpty(LocalPath); } }
        public string VSSPath { get; private set; }
        public string LocalPath { get; private set; }
        public string NugetPackageName { get; private set; }
        public string TestResultFileName { get; private set; }
        public string NunitTestResultFileName { get; private set; }
        public string BuildConfiguration { get; private set; }
        public CoreClrProject(string vssPath, string localPath, string releaseVersion, string buildConfiguration)
        {
            this.VSSPath = vssPath;
            this.LocalPath = localPath;
            this.BuildConfiguration = buildConfiguration;
            string[] paths = localPath.Split('\\');
            if (paths.Length > 0) {
                string projectName = paths[paths.Length - 1];
                this.NugetPackageName = string.Format("{0}_.{1}.nupkg", projectName, releaseVersion);
                this.TestResultFileName = string.Format("{0}-TestResult.xml", projectName);
                this.NunitTestResultFileName = string.Format("{0}-NunitTestResult.xml", projectName);
            }
        }
    }
}
