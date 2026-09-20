#nullable enable
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Hcxmmx.FlandreScarletMod.Scripts;

[ModInitializer(nameof(Init))]
public static class Entry
{
    public static void Init()
    {
        var harmony = new Harmony("sts2.hcxmmx.flandrescarlet");
        harmony.PatchAll();

        // 让游戏能够识别 PCK 场景中挂载的自定义 C# 脚本。
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
        FlandreCombat.RequestScenePreload();
        Log.Info("Flandre Scarlet visual mod initialized.");
    }
}
