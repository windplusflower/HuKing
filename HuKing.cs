/*
 * 空洞骑士Mod入门到进阶指南/配套模版
 * 作者：近环（https://space.bilibili.com/1224243724）
 */

using System.Runtime.ConstrainedExecution;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using Modding;
using Satchel;
using Satchel.Futils;
using UnityEngine;

namespace HuKing;

// Mod配置类，目前只有开关的配置。可以自行添加额外选项，并在GetMenuData里添加交互。
[Serializable]
public class Settings {
    public bool on = true;
    public bool lowPerformanceMode = false;
    public bool enableShining = true;
}

public class HuKing : Mod, IGlobalSettings<Settings>, IMenuMod {
    public static HuKing instance;

    private GameObject sawPrefab;
    private GameObject spikePrefab;
    private GameObject ringPrefab;
    private GameObject beamPrefab;
    private GameObject stomperPrefab;
    /*  
     * ******** Mod名字和版本号 ********
     */
    public HuKing() : base("HuKing") {
        instance = this;
    }
    public override string GetVersion() => "1.0";

    /* 
     * ******** 预加载和hook ********
     */
    public override List<(string, string)> GetPreloadNames() {
        // 预加载你想要的攻击特效或者敌人，具体请阅读教程。
        return new List<(string, string)> {
            ("White_Palace_05", "wp_saw"),
            ("White_Palace_03_hub", "White_ Spikes"),
            ("GG_Ghost_Hu", "Ring Holder/1"),
            ("GG_Radiance","Boss Control/Absolute Radiance/Eye Beam Glow/Burst 1/Radiant Beam"),
            ("Mines_19","_Scenery/stomper_1/mines_stomper_02")
        };
    }
    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
        // 添加需要使用的hooks
        On.PlayMakerFSM.OnEnable += PlayMakerFSM_OnEnable;
        sawPrefab = preloadedObjects["White_Palace_05"]["wp_saw"];
        spikePrefab = preloadedObjects["White_Palace_03_hub"]["White_ Spikes"];
        ringPrefab = preloadedObjects["GG_Ghost_Hu"]["Ring Holder/1"];
        beamPrefab = preloadedObjects["GG_Radiance"]["Boss Control/Absolute Radiance/Eye Beam Glow/Burst 1/Radiant Beam"];
        stomperPrefab = preloadedObjects["Mines_19"]["_Scenery/stomper_1/mines_stomper_02"];

        ModHooks.LanguageGetHook += changeName;
    }
    private string changeName(string key, string title, string orig) {
        if ((key == "GH_HU_C_MAIN" || key == "GH_HU_NC_MAIN" || key == "NAME_GHOST_HU") && mySettings.on) {
            return "胡王";
        }
        return orig;
    }

    /* 
     * ******** FSM相关改动，这个示例改动使得左特随机在空中多次假动作 ********
     */
    [Obsolete]
    private void PlayMakerFSM_OnEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
        if (mySettings.on) {
            if (self.gameObject.scene.name == "GG_Ghost_Hu" && self.gameObject.name == "Ghost Warrior Hu") {
                if (self.FsmName == "Attacking") {
                    Log("enable HuKing");
                    self.enabled = false;
                    if (mySettings.lowPerformanceMode) {
                        self.gameObject.AddComponent<HuStateMachine>().init(spikePrefab, ringPrefab, beamPrefab, stomperPrefab, 0.7f, mySettings.enableShining);
                    }
                    else {
                        self.gameObject.AddComponent<HuStateMachine>().init(sawPrefab, ringPrefab, beamPrefab, stomperPrefab, 0.3f, mySettings.enableShining);
                    }
                }
                if (self.FsmName == "Movement") {
                    self.enabled = false;
                }

            }
        }
        orig(self);
    }

    /* 
     * ******** 配置文件读取和菜单设置，如没有额外需求不需要改动 ********
     */
    private Settings mySettings = new();
    public bool ToggleButtonInsideMenu => true;
    // 读取配置文件
    public void OnLoadGlobal(Settings settings) => mySettings = settings;
    // 写入配置文件
    public Settings OnSaveGlobal() => mySettings;
    // 设置菜单格式
    public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? menu) {
        List<IMenuMod.MenuEntry> menus = new();
        menus.Add(
            new() {
                // 这是个单选菜单，这里提供开和关两种选择。
                Values = new string[]
                {
                    Language.Language.Get("MOH_ON", "MainMenu"),
                    Language.Language.Get("MOH_OFF", "MainMenu"),
                },
                // 把菜单的当前被选项更新到配置变量
                Saver = i => mySettings.on = i == 0,
                Loader = () => mySettings.on ? 0 : 1,
                Name = "胡王",
            }
        );
        menus.Add(
            new() {
                Values = new string[]
                {
                    Language.Language.Get("MOH_ON", "MainMenu"),
                    Language.Language.Get("MOH_OFF", "MainMenu"),
                },
                Saver = i => mySettings.lowPerformanceMode = i == 0,
                Loader = () => mySettings.lowPerformanceMode ? 0 : 1,
                Name = "低性能模式",
            }
        );
        menus.Add(
            new() {
                Values = new string[]
                {
                    Language.Language.Get("MOH_ON", "MainMenu"),
                    Language.Language.Get("MOH_OFF", "MainMenu"),
                },
                Saver = i => mySettings.enableShining = i == 0,
                Loader = () => mySettings.enableShining ? 0 : 1,
                Name = "闪光特效",
            }
        );
        return menus;
    }
}
