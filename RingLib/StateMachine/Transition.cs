using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingLib.StateMachine
{
    public class Transition { }

    public class StateTransition : Transition { }

    public class NoTransition : StateTransition { }

    public class ToState : StateTransition
    {
        public string State;
    }

    public class CoroutineTransition : Transition
    {
        public IEnumerator<Transition>[] RoutinesInternal;
        public object[] Routines
        {
            set
            {
                List<IEnumerator<Transition>> routines = new();
                foreach (var v in value)
                {
                    if (v is IEnumerator<Transition> routine)
                    {
                        routines.Add(routine);
                    }
                    else if (v is CoroutineTransition coroutineTransition)
                    {
                        routines.AddRange(coroutineTransition.RoutinesInternal);
                    }
                    else
                    {
                        Log.LogError(GetType().Name, $"Invalid routine type {v.GetType().Name}");
                    }
                }
                RoutinesInternal = routines.ToArray();
            }
        }
        public object Routine
        {
            set
            {
                Routines = new object[] { value };
            }
        }
    }

    public class WaitFor : CoroutineTransition
    {
        public float Seconds
        {
            set
            {
                IEnumerator<Transition> routine()
                {
                    var timer = value;
                    while (timer > 0)
                    {
                        timer -= Time.deltaTime;
                        yield return new NoTransition();
                    }
                }
                Routine = routine();
            }
        }
    }

    public class WaitForRealtime : CoroutineTransition
    {
        public float Seconds
        {
            set
            {
                IEnumerator<Transition> routine()
                {
                    var timer = value;
                    while (timer > 0)
                    {
                        timer -= Time.unscaledDeltaTime;
                        yield return new NoTransition();
                    }
                }
                Routine = routine();
            }
        }
    }

    public class WaitTill : CoroutineTransition
    {
        public Func<bool> Condition
        {
            set
            {
                IEnumerator<Transition> routine()
                {
                    while (!value())
                    {
                        yield return new NoTransition();
                    }
                }
                Routine = routine();
            }
        }
    }
}
