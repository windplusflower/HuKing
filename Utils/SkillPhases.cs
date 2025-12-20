using RingLib.StateMachine;
public class SkillPhases
{
    //同步用环的，异步用原生的
    public System.Func<IEnumerator<Transition>> Appear;
    public System.Func<System.Collections.IEnumerator> Loop;
    public System.Func<IEnumerator<Transition>> Disappear;

    public SkillPhases(
        System.Func<IEnumerator<Transition>> appear,
        System.Func<System.Collections.IEnumerator> loop,
        System.Func<IEnumerator<Transition>> disappear)
    {
        Appear = appear;
        Loop = loop;
        Disappear = disappear;
    }
}