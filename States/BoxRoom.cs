using System.Collections;
using RingLib.StateMachine;
using Satchel;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HuKing;

internal partial class HuStateMachine : EntityStateMachine
{
    private float speedRate;
    [State]
    private IEnumerator<Transition> BoxRoom()
    {
        yield return new ToState { State = nameof(Appear) };
    }
    private void registerBoxRoom()
    {
        skillTable.Add(nameof(BoxRoom), new SkillPhases(
            () => Appear_BoxRoom(),
            () => Loop_BoxRoom(),
            () => Disappear_BoxRoom()
        ));
        registerSkill(nameof(BoxRoom));
    }
    private IEnumerator Loop_BoxRoom()
    {
        mCoroutines.Add(StartCoroutine(ShotBeamLoop()));
        mCoroutines.Add(StartCoroutine(StomperLoop()));
        yield return null;
    }
    private IEnumerator ShotBeamLoop()
    {
        while (true)
        {
            var damageHero = beam.GetComponent<DamageHero>();
            if (damageHero == null)
            {
                // 有些激光 Prefab 伤害组件在子物体上
                damageHero = beam.GetComponentInChildren<DamageHero>();
            }

            if (damageHero != null)
            {
                damageHero.damageDealt = 1;
            }
            beam.transform.position = transform.position;
            var dir = (Target().transform.position - transform.position).normalized;
            var degree = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            beam.transform.rotation = Quaternion.Euler(0, 0, degree);
            beam.SetActive(true);
            var fsm = beam.LocateMyFSM("Control");
            fsm.SendEvent("ANTIC");
            yield return new WaitForSeconds(0.65f);
            fsm.SendEvent("FIRE");
            yield return new WaitForSeconds(0.1f);
            fsm.SendEvent("END");
            yield return new WaitForSeconds(1f);
            beam.SetActive(false);
            yield return null;
        }
    }
    private IEnumerator StomperLoop()
    {
        speedRate = 5f;
        while (true)
        {
            var isRandom = level > 2;
            var stomper = stompers.Dequeue();
            var heightScale = UnityEngine.Random.Range(1f, 2.8f);
            stomper.GetComponent<StomperStateMachine>().init(heightScale, -1f, speedRate, isRandom);
            stomper.SetActive(true);
            stompers.Enqueue(stomper);

            if (level > 1)
            {
                stomper = stompers.Dequeue();
                heightScale = UnityEngine.Random.Range(1f, 3.8f - heightScale);
                stomper.GetComponent<StomperStateMachine>().init(heightScale, 1f, speedRate, isRandom);
                stomper.SetActive(true);
                stompers.Enqueue(stomper);
            }
            if (speedRate > 1f)
            {
                speedRate -= 1f;
                yield return null;
            }
            else
            {
                speedRate = 1f;
                yield return new WaitForSeconds(1f);
            }
        }
    }
    private IEnumerator<Transition> Appear_BoxRoom()
    {
        yield return null;
    }
    private IEnumerator<Transition> Disappear_BoxRoom()
    {
        foreach (var stomper in stompers)
        {
            stomper.SetActive(false);
        }
        beam.SetActive(false);
        yield return null;
    }
}