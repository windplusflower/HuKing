using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    [State]
    private IEnumerator<Transition> DisAppear()
    {
        warpOut.SetActive(true);
        flash.SetActive(true);
        gameObject.transform.localScale = Vector3.zero;

        if (skillTable.TryGetValue(SkillChoosen, out var skill))
        {
            yield return new CoroutineTransition { Routine = skill.Disappear() };
        }
        foreach (var coroutine in mCoroutines)
        {
            StopCoroutine(coroutine);
        }
        mCoroutines.Clear();

        if (HPManager.hp < 100) yield return new ToState { State = nameof(Death) };
        yield return new ToState { State = nameof(Choose) };
    }
}