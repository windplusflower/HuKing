using RingLib.StateMachine;
using RingLib.Utils;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private List<string> registeredSkills = new List<string>();
    private Dictionary<string, int> skillUsageCount = new Dictionary<string, int>();
    [State]
    private IEnumerator<Transition> Choose()
    {
        if (registeredSkills.Count == 0)
        {
            HuKing.instance.LogError("No skills registered!");
            yield return new ToState { State = nameof(Idle) };
            yield break;
        }

        int minCount = skillUsageCount.Values.Min();

        List<string> candidates = skillUsageCount
            .Where(kvp => kvp.Value == minCount)
            .Select(kvp => kvp.Key)
            .ToList();

        if (candidates.Count > 1 && !string.IsNullOrEmpty(SkillChoosen))
        {
            candidates.Remove(SkillChoosen);
        }

        // 4. 随机选择
        string nextSkill = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        // 5. 更新状态
        SkillChoosen = nextSkill;
        skillUsageCount[nextSkill]++;

        // 6. Level 升级逻辑
        if (HPManager.hp <= originalHp * 2 / 3) level = 2;
        if (HPManager.hp <= originalHp / 3) level = 3;

        yield return new ToState { State = SkillChoosen };
    }
    public void registerSkill(string skillName)
    {
        if (!registeredSkills.Contains(skillName))
        {
            registeredSkills.Add(skillName);
            skillUsageCount[skillName] = 0;
        }
    }
}