﻿using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.ButterLib.CampaignIdentifier;
using Bannerlord.ButterLib.Common.Extensions;
using Bannerlord.ButterLib.CrashUploader;
using Bannerlord.ButterLib.DelayedSubModule;
using Bannerlord.ButterLib.ExceptionHandler;
using Bannerlord.ButterLib.ObjectSystem.Extensions;
using Bannerlord.ButterLib.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Diagnostics;
using System.Diagnostics.Logger;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.ButterLib
{
    /// <summary>
    /// Main SubModule. Performs initialization of all 3 stages.
    /// </summary>
    public sealed partial class ButterLibSubModule : MBSubModuleBase
    {
        private const string SWarningTitle =
@"{=BguqytVG3q}Warning from Bannerlord.ButterLib!";
        private const string SErrorHarmonyNotFound =
@"{=EEVJa5azpB}Bannerlord.Harmony module was not found!";
        private const string SErrorModuleLoaderNotFound =
@"{=j3DZ87zFMB}Bannerlord.ModuleLoader module was not found!";
        private const string SErrorButterLibNotFound =
@"{=5EDzm7u4mS}Bannerlord.ButterLib module was not found!";
        private const string SErrorOfficialModulesLoadedBeforeButterLib =
@"{=GDkjThJcH6}ButterLib is loaded after the official modules!
Make sure ButterLib is loaded before them!";
        private const string SErrorOfficialModules =
@"{=5k4Eqevh53}The following modules were loaded before ButterLib:";
        private const string SMessageContinue =
@"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";

        internal event Action<float>? OnApplicationTickEvent;

        private ILogger Logger { get; set; } = default!;
        private bool DelayedServiceCreation { get; set; }
        private bool ServiceRegistrationWasCalled { get; set; }
        private bool OnBeforeInitialModuleScreenSetAsRootWasCalled { get; set; }

        private TextWriterTraceListener? TextWriterTraceListener { get; set; }

        public ButterLibSubModule()
        {
            Instance = this;

            CheckLoadOrder();
        }

        public void OnServiceRegistration()
        {
            ServiceRegistrationWasCalled = true;

            CanBeConfigured = false;

            Services = new ServiceCollection();
            Services.AddOptions();
            Services.Configure<ButterLibOptions>(o =>
            {
                var defaultJsonOptions = new JsonButterLibOptionsModel();
                o.MinLogLevel = defaultJsonOptions.MinLogLevel;
            });

            foreach (var action in BeforeInitialization)
                action?.Invoke(Services);

            this.AddDefaultSerilogLogger();
            this.AddSerilogLoggerProvider("butterlib.txt", new[] { "Bannerlord.ButterLib.*" });
            this.AddSerilogLoggerProvider("trace.txt", new[] { "System.Diagnostics.Logger.*" });

            Services.AddSubSystem<DelayedSubModuleSubSystem>();
            Services.AddSubSystem<ExceptionHandlerSubSystem>();
            Services.AddSubSystem<CrashUploaderSubSystem>();

            Services.AddSingleton<ICrashUploader, BUTRCrashUploader>();
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            IServiceProvider serviceProvider;

            if (!ServiceRegistrationWasCalled)
            {
                OnServiceRegistration();
                DelayedServiceCreation = true;
                serviceProvider = this.GetTempServiceProvider()!;
            }
            else
            {
                serviceProvider = this.GetServiceProvider()!;
            }

            Logger = serviceProvider.GetRequiredService<ILogger<ButterLibSubModule>>();
            Logger.LogTrace("OnSubModuleLoad: Logging started...");

            if (!DelayedServiceCreation)
                InitializeServices();

            ExceptionHandlerSubSystem.Instance?.Enable();
            CrashUploaderSubSystem.Instance?.Enable();

            Trace.Listeners.Add(TextWriterTraceListener = new TextWriterTraceListener(new StreamWriter(new MemoryStream(), Encoding.UTF8, 1024, true)));
            Trace.AutoFlush = true;
            Logger.LogTrace("Added System.Diagnostics.Trace temporary listener.");

            Logger.LogTrace("OnSubModuleLoad: Done");
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            Logger.LogTrace("OnSubModuleUnloaded: Started...");

            Instance = null!;

            Logger.LogTrace("OnSubModuleUnloaded: Done");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Logger.LogTrace("OnBeforeInitialModuleScreenSetAsRoot: Started...");

            if (!OnBeforeInitialModuleScreenSetAsRootWasCalled)
            {
                OnBeforeInitialModuleScreenSetAsRootWasCalled = true;

                if (DelayedServiceCreation)
                {
                    InitializeServices();
                }
            }

            Logger.LogTrace("OnBeforeInitialModuleScreenSetAsRoot: Done");
        }

        protected override void OnApplicationTick(float dt) => OnApplicationTickEvent?.Invoke(dt);

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            Logger.LogTrace("OnGameStart: Started");

            GameScope = ServiceProvider.CreateScope();
            Logger.LogInformation("Created GameScope.");

            if (game.GameType is Campaign)
                CampaignIdentifierEvents.Instance = new CampaignIdentifierEvents();

            Logger.LogTrace("OnGameStart: Done");
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            Logger.LogTrace("OnGameEnd: Started");

            GameScope = null;

            if (game.GameType is Campaign)
            {
                MBObjectBaseExtensions.OnGameEnd();
                CampaignIdentifierEvents.Instance = null;
            }

            Logger.LogTrace("OnGameEnd: Done");
        }


        private static void CheckLoadOrder()
        {
            var loadedModules = BUTR.Shared.Helpers.ModuleInfoHelper.GetLoadedModules().ToList();
            if (loadedModules.Count == 0) return;

            var sb = new StringBuilder();

            var harmonyModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.Harmony");

            var harmonyModuleIndex = harmonyModule is not null ? loadedModules.IndexOf(harmonyModule) : -1;
            if (harmonyModuleIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SErrorHarmonyNotFound)?.ToString());
            }

            // TODO: Keep it optional for now
            /*
            var moduleLoaderModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.ModuleLoader");
            var moduleLoaderIndex = moduleLoaderModule is not null ? loadedModules.IndexOf(moduleLoaderModule) : -1;
            if (moduleLoaderIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create((SErrorModuleLoaderNotFound).ToString());
            }
            */

            var butterLibModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.ButterLib");
            var butterLibModuleIndex = butterLibModule is not null ? loadedModules.IndexOf(butterLibModule) : -1;
            if (butterLibModuleIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SErrorButterLibNotFound)?.ToString());
            }

            var officialModules = loadedModules.Where(x => x.IsOfficial).Select(x => (Module: x, Index: loadedModules.IndexOf(x)));
            var modulesLoadedBeforeButterLib = officialModules.Where(tuple => tuple.Index < butterLibModuleIndex).ToList();
            if (modulesLoadedBeforeButterLib.Count > 0)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SErrorOfficialModulesLoadedBeforeButterLib)?.ToString());
                sb.AppendLine(TextObjectHelper.Create(SErrorOfficialModules)?.ToString());
                foreach (var (module, _) in modulesLoadedBeforeButterLib)
                    sb.AppendLine(module.Id);
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SMessageContinue)?.ToString());

                switch (MessageBox.Show(sb.ToString(), TextObjectHelper.Create(SWarningTitle)?.ToString(), MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        Environment.Exit(1);
                        break;
                }
            }
        }

        private void InitializeServices()
        {
            if (Services is not null)
            {
                GlobalServiceProvider = Services.BuildServiceProvider();
                Logger.LogTrace("Created GlobalServiceProvider.");
                Services = null!;
                Logger.LogTrace("Set Services to null.");

                Logger = this.GetServiceProvider().GetRequiredService<ILogger<ButterLibSubModule>>();
                Logger.LogTrace("Assigned new _logger from GlobalServiceProvider.");

                var logger = this.GetServiceProvider().GetRequiredService<ILogger<LoggerTraceListener>>();
                Trace.Listeners.Add(new LoggerTraceListener(logger));
                Logger.LogTrace("Added System.Diagnostics.Trace main listener.");

                if (TextWriterTraceListener is not null)
                {
                    try
                    {
                        Trace.Flush(); // In case AutoFlush was set to false
                        Trace.Listeners.Remove(TextWriterTraceListener);
                        if (TextWriterTraceListener.Writer is StreamWriter { BaseStream: MemoryStream ms })
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            using var reader = new StreamReader(ms, Encoding.UTF8, true, 1024, true);
                            while (reader.Peek() >= 0)
                            {
                                Trace.WriteLine(reader.ReadLine());
                            }
                            Logger.LogTrace("Flushed logs from the System.Diagnostics.Trace temp listener.");
                        }
                    }
                    finally
                    {
                        TextWriterTraceListener.Dispose();
                    }
                }
            }
        }
    }
}