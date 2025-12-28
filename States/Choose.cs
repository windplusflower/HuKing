using RingLib.StateMachine;
using RingLib.Utils;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private RandomSelector<string> selector = new([
        // new(nameof(SawRoom),1,2,5),
        // new(nameof(BoxRoom),1,2,5),
        // new(nameof(SawNailRoom),1,2,5)
        new(nameof(SawShotRoom),1,2,5),
    ]);
    [State]
    private IEnumerator<Transition> Choose()
    {
        SkillChoosen = selector.Get();
        if (HPManager.hp <= originalHp * 2 / 3) level = 2;
        if (HPManager.hp <= originalHp / 3) level = 3;
        yield return new ToState { State = SkillChoosen };
    }
}