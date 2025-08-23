using System.Collections;
using System.Collections.Generic;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine {
    private List<UnityEngine.Coroutine> mCoroutines = new List<UnityEngine.Coroutine>();
    [State]
    private IEnumerator<Transition> Hit() {
        //HuKing.instance.Log("Hit state entered");
        mCoroutines.Add(StartCoroutine(skillLoop()));
        yield return new CoroutineTransition { Routine = WaitForHit() };
        yield return new ToState { State = nameof(DisAppear) };
    }
    private IEnumerator<Transition> WaitForHit() {
        yield return new WaitTill {
            Condition = () => {
                if (HPManager.hp % 100 != 1) {
                    int damage = (originalHp - 100 * hitCount + 1) - HPManager.hp;
                    if (damage % 5 == 0) {
                        HPManager.hp = originalHp - 100 * hitCount + 1;
                        return false;
                    }
                    else {
                        hitCount++;
                        HPManager.hp = originalHp - 100 * hitCount + 1;
                        return true;
                    }
                }
                return false;
            }
        };
    }
    private IEnumerator skillLoop() {
        switch (SkillChoosen) {
            case "SawRoom":
                yield return SawLoop();
                break;
            case "BoxRoom":
                yield return BoxLoop();
                break;
            default:
                yield return null;
                break;
        }
    }
}