using RingLib.StateMachine;
using RingLib.Utils;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private RandomSelector<string> selector = new([
        new(nameof(SawRoom),1,2,3),
        new(nameof(BoxRoom),1,2,3),
        // new(nameof(SawNailRoom),0,2,3)
    ]);
    [State]
    private IEnumerator<Transition> Choose()
    {
        SkillChoosen = selector.Get();
        HuKing.instance.Log($"Choosing next state {SkillChoosen}");
        yield return new ToState { State = SkillChoosen };
    }
}