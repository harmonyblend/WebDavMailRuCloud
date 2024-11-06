﻿using System;
using System.Reflection;
using CommandLine;
using WinServiceInstaller;

namespace YaR.Clouds.Console
{
    public class Program
    {
        private static ServiceConfigurator _c;

        private static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);

            var exitCode = result
                .MapResult(
                    (CommandLineOptions options) =>
                    {
                        _c = new ServiceConfigurator
                        {
                            Assembly = Assembly.GetExecutingAssembly(),
                            Name = options.ServiceInstall ?? options.ServiceUninstall ?? "wdmrc",
                            DisplayName = string.IsNullOrEmpty(options.ServiceInstallDisplayName)
                                ? $"WebDavCloud [port {string.Join(", port ", options.Port)}]"
                                : options.ServiceInstallDisplayName,
                            Description = "WebDAV emulator for cloud.Mail.ru & disk.Yandex.ru",

                            FireStart = () => Payload.Run(options),
                            FireStop = Payload.Stop

                        };

                        if (options.ServiceInstall != null)
                        {
                            options.ServiceRun = true;
                            options.ServiceInstall = null;
                            _c.CommandLine = Parser.Default.FormatCommandLine(options);

                            try
                            {
                                _c.Install();
                                return 0;
                            }
                            catch (Exception ex)
                            {
                                System.Console.Error.WriteLine(ex.Message);
                                return 1;
                            }
                        }

                        if (options.ServiceUninstall != null)
                        {
                            try
                            {
                                _c.Uninstall();
                                return 0;
                            }
                            catch (Exception ex)
                            {
                                System.Console.Error.WriteLine(ex.Message);
                                return 1;
                            }
                        }

                        if (options.ServiceRun)
                        {
                            _c.Run();
                            return 0;
                        }

                        System.Console.CancelKeyPress += (_, _) => Payload.Stop();
                        Payload.Run(options);
                        return 0;
                    },
                    _ => 1);

            if (exitCode > 0) Environment.Exit(exitCode);
        }

    }
}
