﻿

namespace StatLight.Console
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.ServiceModel;
    using StatLight.Console.Tools;
    using StatLight.Core.Configuration;
    using StatLight.Core.Common;
    using StatLight.Core.Reporting;
    using StatLight.Core.Reporting.Providers.Xml;
    using StatLight.Core.Runners;
    using StatLight.Core.WebServer.XapHost;
    using StatLight.Core.UnitTestProviders;
    using System.Collections.Generic;

    class Program
    {
        const int ExitFailed = 1;
        const int ExitSucceeded = 0;

        static void Main(string[] args)
        {
            PrintNameVersionAndCopyright();

#if DEBUG
            ILogger logger = new ConsoleLogger(LogChatterLevels.Full);
#else
			ILogger logger = new ConsoleLogger(LogChatterLevels.Error | LogChatterLevels.Warning | LogChatterLevels.Information);
#endif

            ArgOptions options;

            using (var consoleIconSwapper = new ConsoleIconSwapper())
            {
                consoleIconSwapper.ShowConsoleIcon(CoreResources.FavIcon);

                try
                {
                    options = new ArgOptions(args);
                    if (options.ShowHelp)
                    {
                        ArgOptions.ShowHelpMessage(Console.Out, options);
                        return;
                    }

                    string xapPath = options.XapPath;
                    bool continuousIntegrationMode = options.ContinuousIntegrationMode;
                    bool showTestingBrowserHost = options.ShowTestingBrowserHost;
                    bool useTeamCity = options.OutputForTeamCity;
                    bool startWebServerOnly = options.StartWebServerOnly;
                    List<string> methodsToTest = options.MethodsToTest;
                    string xmlReportOutputPath = options.XmlReportOutputPath;
                    MicrosoftTestingFrameworkVersion? microsoftTestingFrameworkVersion = options.MicrosoftTestingFrameworkVersion;
                    string tagFilters = options.TagFilters;
                    UnitTestProviderType unitTestProviderType = options.UnitTestProviderType;

                    var statLightRunnerFactory = new StatLightRunnerFactory();
                    var statLightConfigurationFactory = new StatLightConfigurationFactory(logger);

                    StatLightConfiguration statLightConfiguration = statLightConfigurationFactory
                        .GetStatLightConfiguration(
                            unitTestProviderType,
                            xapPath,
                            microsoftTestingFrameworkVersion,
                            methodsToTest,
                            tagFilters);

                    var runnerType = DetermineRunnerType(continuousIntegrationMode, useTeamCity, startWebServerOnly);

                    var runner = GetRunner(
                            logger,
                            runnerType,
                            showTestingBrowserHost,
                            statLightConfiguration,
                            statLightRunnerFactory);

                    TestReport testReport = runner.Run();

                    if (!string.IsNullOrEmpty(xmlReportOutputPath))
                    {
                        var xmlReport = new XmlReport(testReport, xapPath);
                        xmlReport.WriteXmlReport(xmlReportOutputPath);

                        "*********************************"
                            .WrapConsoleMessageWithColor(ConsoleColor.White, true);

                        "Wrote XML report to:{0}{1}"
                            .FormatWith(Environment.NewLine, new FileInfo(xmlReportOutputPath).FullName)
                            .WrapConsoleMessageWithColor(ConsoleColor.Yellow, true);

                        "*********************************"
                            .WrapConsoleMessageWithColor(ConsoleColor.White, true);
                    }

                    if (testReport.FinalResult == RunCompletedState.Failure)
                        Environment.ExitCode = ExitFailed;
                    else
                        Environment.ExitCode = ExitSucceeded;

                }
                catch (AddressAccessDeniedException addressAccessDeniedException)
                {
                    Environment.ExitCode = ExitFailed;
                    var helpMessage = @"
Cannot run StatLight. The current account does not have the correct privilages.

Exception:
{0}

Try: (the following two steps that should allow StatLight to start a web server on the requested port)
     1. Run cmd.exe as Administrator.
     2. Enter the following command in the command prompt.
          netsh http add urlacl url=http://+:8887/ user=DOMAIN\user
".FormatWith(addressAccessDeniedException.Message);

                    WriteErrorToConsole(helpMessage, "Error");
                }
                catch (FileNotFoundException fileNotFoundException)
                {
                    HandleKnownError(fileNotFoundException);
                }
                catch (StatLightException statLightException)
                {
                    HandleKnownError(statLightException);
                }

                catch (Exception exception)
                {
                    HandleUnknownError(exception);
                }
            }
        }

        private static void HandleUnknownError(Exception exception)
        {
            Environment.ExitCode = ExitFailed;
            WriteErrorToConsole(exception.ToString(), "Unexpected Error");
        }

        private static void HandleKnownError(Exception optionException)
        {
            Environment.ExitCode = ExitFailed;
            WriteErrorToConsole(optionException.Message, "Error");
        }


        private static void WriteErrorToConsole(string errorMessage, string beginMsg)
        {
            Write("");
            Write("");
            ArgOptions.ShowHelpMessage(Console.Out);
            Write("");
            Write("************* " + beginMsg + " *************");
            errorMessage.WrapConsoleMessageWithColor(ConsoleColor.Red, true);
            Write("*********************************");
        }

        private static IRunner GetRunner(ILogger logger, RunnerType runnerType, bool showTestingBrowserHost,
            StatLightConfiguration statLightConfiguration, StatLightRunnerFactory statLightRunnerFactory)
        {
            switch (runnerType)
            {
                case RunnerType.TeamCity:
                    logger.LogChatterLevel = LogChatterLevels.None;
                    return statLightRunnerFactory.CreateTeamCityRunner(statLightConfiguration);

                case RunnerType.ContinuousTest:
                    return statLightRunnerFactory.CreateContinuousTestRunner(logger, statLightConfiguration, showTestingBrowserHost);

                case RunnerType.WebServerOnly:
                    return statLightRunnerFactory.CreateWebServerOnlyRunner(logger, statLightConfiguration);

                default:
                    return statLightRunnerFactory.CreateOnetimeConsoleRunner(logger, statLightConfiguration, showTestingBrowserHost);
            }
        }

        private static RunnerType DetermineRunnerType(bool continuousIntegrationMode,
            bool useTeamCity,
            bool startWebServerOnly)
        {
            if (useTeamCity)
            {
                return RunnerType.TeamCity;
            }

            if (startWebServerOnly)
            {
                return RunnerType.WebServerOnly;
            }

            if (continuousIntegrationMode)
            {
                return RunnerType.ContinuousTest;
            }

            return RunnerType.OneTimeConsole;
        }

        private static void PrintNameVersionAndCopyright()
        {
            var version = Assembly
                .GetEntryAssembly()
                .GetName()
                .Version;

            Write("");
            Write("StatLight - Version {0}.{1}.{2}", version.Major, version.Minor, version.Build);
            Write("Copyright (C) 2009 Jason Jarrett");
            Write("All Rights Reserved.");
            Write("");
        }

        private static void Write(string msg, params object[] args)
        {
            Console.WriteLine(msg, args);
        }
    }
}
