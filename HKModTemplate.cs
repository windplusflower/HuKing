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

namespace MossJumper;

// Mod配置类，目前只有开关的配置。可以自行添加额外选项，并在GetMenuData里添加交互。
[Serializable]
public class Settings {
    public bool on = true;
}

public class HKModTemplate : Mod, IGlobalSettings<Settings>, IMenuMod {
    public static HKModTemplate instance;

    private Dictionary<string, int> skillWeights = new Dictionary<string, int>();

    private SkillSelector selector;
    /*  
     * ******** Mod名字和版本号 ********
     */
    public HKModTemplate() : base("HKModTemplate") {
        instance = this;
    }
    public override string GetVersion() => "1.0";

    /* 
     * ******** 预加载和hook ********
     */
    public override List<(string, string)> GetPreloadNames() {
        // 预加载你想要的攻击特效或者敌人，具体请阅读教程。
        return new List<(string, string)> {
            // ("GG_Radiance", "Boss Control/Absolute Radiance")
        };
    }
    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
        // 添加需要使用的hooks
        On.PlayMakerFSM.OnEnable += PlayMakerFSM_OnEnable;
        // var radiance = preloadedObjects["GG_Radiance"]["Boss Control/Absolute Radiance"];
        // var radianceFSM = radiance.LocateMyFSM("Attack Commands");
        // orbTemplate = radianceFSM.GetAction<SpawnObjectFromGlobalPool>("Spawn Fireball", 1).gameObject.Value;
        // GameObject.Destroy(orbTemplate.GetComponent<PersistentBoolItem>());
        // GameObject.Destroy(orbTemplate.GetComponent<ConstrainPosition>());
        // OrbShotting.orbTemplate = orbTemplate;

        ModHooks.LanguageGetHook += changeName;
    }
    private string changeName(string key, string title, string orig) {
        // if (key == "MEGA_MOSS_MAIN" || key == "NAME_MEGA_MOSS_CHARGER" && mySettings.on) {
        //     return "大型苔藓冲飞者";
        // }
        return orig;
    }

    /* 
     * ******** FSM相关改动，这个示例改动使得左特随机在空中多次假动作 ********
     */
    [Obsolete]
    private void PlayMakerFSM_OnEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
        if (mySettings.on) {
            //FSM:MoveMent Attacking BroadcastDeath
            if (self.gameObject.scene.name == "GG_Ghost_Hu" && self.gameObject.name == "Ghost Warrior Hu") {
                if (self.FsmName == "Attacking") {
                    self.enabled = false;
                }
                if (self.FsmName == "MoveMent") {
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
                // Name = "Moss Jumper",
            }
        );
        return menus;
    }
}
