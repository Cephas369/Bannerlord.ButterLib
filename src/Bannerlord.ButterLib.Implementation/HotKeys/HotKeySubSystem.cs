﻿/*
 * Original code from https://github.com/sirdoombox/BannerLib/blob/d721fb572f33a702ab7b724b866fe06d86d60d1a/BannerLib.Input/InputSubModule.cs.
 * Licensed under Unlicense License.
 * Authors: sirdoombox, BUTR.
 */

using Bannerlord.ButterLib.SubSystems;

using TaleWorlds.InputSystem;

namespace Bannerlord.ButterLib.Implementation.HotKeys
{
    internal sealed class HotKeySubSystem : ISubSystem
    {
        public static HotKeySubSystem? Instance { get; private set; }

        public string Id => "Hot Keys";
        public string Description => "Provides a better way for mods to create hotkeys";
        public bool IsEnabled { get; private set; }
        public bool CanBeDisabled => true;
        public bool CanBeSwitchedAtRuntime => false;

        public HotKeySubSystem()
        {
            Instance = this;
        }

        public void Enable()
        {
            IsEnabled = true;

            if (ButterLibSubModule.Instance is { } instance)
                instance.OnApplicationTickEvent += OnApplicationTick;
        }

        public void Disable()
        {
            IsEnabled = false;

            if (ButterLibSubModule.Instance is { } instance)
                instance.OnApplicationTickEvent -= OnApplicationTick;
        }


        private static void OnApplicationTick(float dt)
        {
            foreach (var hotKey in HotKeyManagerImplementation.HotKeys)
            {
                if (!hotKey.ShouldExecute() || hotKey.GameKey is null) continue;

#if e160 || e161 || e162 || e163 || e164 || e165 || e170 || e171
                if (hotKey.GameKey.KeyboardKey?.InputKey.IsDown() == true || hotKey.GameKey.ControllerKey?.InputKey.IsDown() == true)
                    hotKey.IsDownInternal();
                if (hotKey.GameKey.KeyboardKey?.InputKey.IsPressed() == true || hotKey.GameKey.ControllerKey?.InputKey.IsPressed() == true)
                    hotKey.OnPressedInternal();
                if (hotKey.GameKey.KeyboardKey?.InputKey.IsReleased() == true || hotKey.GameKey.ControllerKey?.InputKey.IsReleased() == true)
                    hotKey.OnReleasedInternal();
#else
#error ConstGameVersionWithPrefix is not handled!
#endif
            }
        }
    }
}