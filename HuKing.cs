
using System.Runtime.ConstrainedExecution;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using Modding;
using Satchel;
using Satchel.Futils;
using UnityEngine;

namespace HuKing;

[Serializable]
public class Settings
{
    public bool on = true;
    public bool lowPerformanceMode = false;
    public bool enableShining = true;
}

public class HuKing : Mod, IGlobalSettings<Settings>, IMenuMod
{
    public static HuKing instance;

    private GameObject sawPrefab;
    private GameObject spikePrefab;
    private GameObject ringPrefab;
    private GameObject beamPrefab;
    private GameObject stomperPrefab;
    private GameObject platPrefab;
    private GameObject nailPrefab;
    /*  
     * ******** Mod名字和版本号 ********
     */
    public HuKing() : base("HuKing")
    {
        instance = this;
    }
    public override string GetVersion() => "1.0";

    /* 
     * ******** 预加载和hook ********
     */
    public override List<(string, string)> GetPreloadNames()
    {
        return new List<(string, string)> {
            ("White_Palace_05", "wp_saw"),
            ("White_Palace_03_hub", "White_ Spikes"),
            ("GG_Ghost_Hu", "Ring Holder/1"),
            ("GG_Radiance","Boss Control/Absolute Radiance/Eye Beam Glow/Burst 1/Radiant Beam"),
            ("Mines_19","_Scenery/stomper_1/mines_stomper_02"),
            ("GG_Workshop", "gg_plat_float_wide"),
            ("GG_Radiance", "Boss Control/Absolute Radiance"),
        };
    }
    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        On.PlayMakerFSM.OnEnable += PlayMakerFSM_OnEnable;
        sawPrefab = preloadedObjects["White_Palace_05"]["wp_saw"];
        spikePrefab = preloadedObjects["White_Palace_03_hub"]["White_ Spikes"];
        ringPrefab = preloadedObjects["GG_Ghost_Hu"]["Ring Holder/1"];
        beamPrefab = preloadedObjects["GG_Radiance"]["Boss Control/Absolute Radiance/Eye Beam Glow/Burst 1/Radiant Beam"];
        stomperPrefab = preloadedObjects["Mines_19"]["_Scenery/stomper_1/mines_stomper_02"];
        platPrefab = preloadedObjects["GG_Workshop"]["gg_plat_float_wide"];

        var radiance = preloadedObjects["GG_Radiance"]["Boss Control/Absolute Radiance"];
        var radianceFSM = radiance.LocateMyFSM("Attack Commands"); var nailComb = radianceFSM.GetAction<SpawnObjectFromGlobalPool>("Comb Top", 0).gameObject.Value;
        var nailCombFSM = nailComb.LocateMyFSM("Control");
        nailPrefab = nailCombFSM.GetAction<SpawnObjectFromGlobalPool>("RG1", 1).gameObject.Value;
        GameObject.Destroy(nailPrefab.GetComponent<PersistentBoolItem>());
        GameObject.Destroy(nailPrefab.GetComponent<ConstrainPosition>());
        ModHooks.LanguageGetHook += changeName;
    }
    private string changeName(string key, string title, string orig)
    {
        if ((key == "GH_HU_C_MAIN" || key == "GH_HU_NC_MAIN" || key == "NAME_GHOST_HU") && mySettings.on)
        {
            return "胡王";
        }
        return orig;
    }

    [Obsolete]
    private void PlayMakerFSM_OnEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
    {
        orig(self); // 先执行原逻辑

        if (mySettings.on)
        {
            if (self.gameObject.scene.name == "GG_Ghost_Hu" && self.gameObject.name == "Ghost Warrior Hu")
            {
                // 关键：检查是否已经添加过我们的状态机，防止重复添加和协程冲突
                if (self.gameObject.GetComponent<HuStateMachine>() == null)
                {
                    if (self.FsmName == "Attacking")
                    {
                        // 彻底关掉原版 FSM，不再让它们反复干扰
                        self.enabled = false;

                        float sSize = mySettings.lowPerformanceMode ? 0.7f : 0.3f;
                        GameObject sPrefab = mySettings.lowPerformanceMode ? spikePrefab : sawPrefab;

                        self.gameObject.AddComponent<HuStateMachine>().init(
                            sPrefab, sawPrefab, ringPrefab, beamPrefab,
                            stomperPrefab, platPrefab, nailPrefab, sSize, mySettings.enableShining);
                    }
                }

                // 如果是已经禁用的 FSM 再次尝试启用，继续保持禁用
                if (self.FsmName == "Attacking" || self.FsmName == "Movement")
                {
                    self.enabled = false;
                }
            }
        }
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
    public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? menu)
    {
        List<IMenuMod.MenuEntry> menus = new();
        menus.Add(
            new()
            {
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
            new()
            {
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
            new()
            {
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
