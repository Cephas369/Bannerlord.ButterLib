﻿using Bannerlord.ButterLib.CampaignIdentifier;
using Bannerlord.ButterLib.Common.Extensions;
using Bannerlord.ButterLib.DistanceMatrix;
using Bannerlord.ButterLib.HotKeys;
using Bannerlord.ButterLib.Implementation.CampaignIdentifier;
using Bannerlord.ButterLib.Implementation.CampaignIdentifier.CampaignBehaviors;
using Bannerlord.ButterLib.Implementation.CampaignIdentifier.Patches;
using Bannerlord.ButterLib.Implementation.Common.Extensions;
using Bannerlord.ButterLib.Implementation.DistanceMatrix;
using Bannerlord.ButterLib.Implementation.HotKeys;
using Bannerlord.ButterLib.Implementation.Logging;
using Bannerlord.ButterLib.Implementation.ObjectSystem;
using Bannerlord.ButterLib.Implementation.ObjectSystem.Patches;
using Bannerlord.ButterLib.Implementation.SaveSystem.Patches;
using Bannerlord.ButterLib.ObjectSystem;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.ButterLib.Implementation
{
    public sealed class SubModule : MBSubModuleBase
    {
        internal static ILogger? Logger { get; private set; }

        private bool ServiceRegistrationWasCalled { get; set; }
        private bool OnBeforeInitialModuleScreenSetAsRootWasCalled { get; set; }

        public void OnServiceRegistration()
        {
            ServiceRegistrationWasCalled = true;

            if (this.GetServices() is { } services)
            {
                services.AddScoped<CampaignDescriptor, CampaignDescriptorImplementation>();
                services.AddSingleton<ICampaignDescriptorStatic, CampaignDescriptorStaticImplementation>();
                services.AddScoped(typeof(DistanceMatrix<>), typeof(DistanceMatrixImplementation<>));
                services.AddSingleton<IDistanceMatrixStatic, DistanceMatrixStaticImplementation>();
                services.AddSingleton<ICampaignExtensions, CampaignExtensionsImplementation>();
                services.AddTransient<ICampaignDescriptorProvider, JsonCampaignDescriptorProvider>();
                services.AddScoped<IMBObjectExtensionDataStore, MBObjectExtensionDataStore>();
                services.AddScoped<HotKeyManager, HotKeyManagerImplementation>();
                services.AddSingleton<IHotKeyManagerStatic, HotKeyManagerStaticImplementation>();
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            if (!ServiceRegistrationWasCalled)
                OnServiceRegistration();

            Logger = this.GetServiceProvider().GetRequiredService<ILogger<SubModule>>();
            Logger.LogTrace("ButterLib.Implementation: OnSubModuleLoad");

            Logger.LogInformation("Wrapping DebugManager of type {type} with DebugManagerWrapper.", Debug.DebugManager.GetType());
            Debug.DebugManager = new DebugManagerWrapper(Debug.DebugManager, this.GetServiceProvider()!);

            HotKeySubSystem.Enable();

            Logger.LogTrace("ButterLib.Implementation: OnSubModuleLoad: Done");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Logger.LogTrace("ButterLib.Implementation: OnBeforeInitialModuleScreenSetAsRoot");

            if (!OnBeforeInitialModuleScreenSetAsRootWasCalled)
            {
                OnBeforeInitialModuleScreenSetAsRootWasCalled = true;

                if (Debug.DebugManager is not DebugManagerWrapper)
                {
                    Logger.LogWarning("DebugManagerWrapper was replaced with {type}! Wrapping it with DebugManagerWrapper.", Debug.DebugManager.GetType());
                    Debug.DebugManager = new DebugManagerWrapper(Debug.DebugManager, this.GetServiceProvider()!);
                }

                var campaignIdentifierHarmony = new Harmony("Bannerlord.ButterLib.CampaignIdentifier");
                CharacterCreationContentApplyCulturePatch.Apply(campaignIdentifierHarmony);
                ClanInitializeClanPatch.Apply(campaignIdentifierHarmony);

                CampaignBehaviorManagerPatch.Apply(new Harmony("Bannerlord.ButterLib.MBObjectExtensionDataStore"));

                var saveSystemHarmony = new Harmony("Bannerlord.ButterLib.SaveSystem");
                TypeExtensionsPatch.Apply(saveSystemHarmony); // Adds support for saving many more container types
                //DefinitionContextPatch.Apply(saveSystemHarmony); // Fixes save corruption & crashes when duplicate types are defined
            }

            Logger.LogTrace("ButterLib.Implementation: OnBeforeInitialModuleScreenSetAsRoot: Done");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            Logger.LogTrace("ButterLib.Implementation: OnGameStart");

            if (game.GameType is Campaign)
            {
                var gameStarter = (CampaignGameStarter)gameStarterObject;

                // Behaviors
                gameStarter.AddBehavior(new CampaignIdentifierBehavior());
                gameStarter.AddBehavior(new GeopoliticsCachingBehavior());
            }

            Logger.LogTrace("ButterLib.Implementation: OnGameStart: Done");
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            Logger.LogTrace("ButterLib.Implementation: OnGameEnd");

            if (game.GameType is Campaign)
            {
                CampaignIdentifierEvents.Instance = null;
            }

            Logger.LogTrace("ButterLib.Implementation: OnGameEnd: Done");
        }
    }
}