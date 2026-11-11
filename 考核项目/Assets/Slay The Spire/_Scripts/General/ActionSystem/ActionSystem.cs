using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionSystem : Singleton<ActionSystem>
{
    private List<GameAction> reactions = null;
    public bool IsPerforming { get; private set; } = false;

    // 订阅字典：PRE执行前 / POST执行后
    private static Dictionary<Type, List<Action<GameAction>>> preSubs = new();
    private static Dictionary<Type, List<Action<GameAction>>> postSubs = new();

    // 动作执行器字典，每种GameAction类型绑定一个Perform协程
    private static Dictionary<Type, Func<GameAction, IEnumerator>> performers = new();


    public void Perform(GameAction action, System.Action OnPerformFinished = null)
    {
        if (IsPerforming) return;
        IsPerforming = true;
        StartCoroutine(Flow(action, () =>
        {
            IsPerforming = false;
            OnPerformFinished?.Invoke();
        }));
    }

    public void AddReaction(GameAction gameAction)
    {
        reactions?.Add(gameAction);
    }


    private IEnumerator Flow(GameAction action, Action OnFlowFinished = null)
    {
        // ========== Pre阶段：动作执行之前 ==========
        reactions = action.PreReactions;
        PerformSubscribers(action, preSubs);
        yield return PerformReactions();

        // ========== Perform阶段：动作本体执行 ==========
        reactions = action.PerformReactions;
        yield return PerformPerformer(action);
        yield return PerformReactions();

        // ========== Post阶段：动作执行完成之后 ==========
        reactions = action.PostReactions;
        PerformSubscribers(action, postSubs);
        yield return PerformReactions();

        OnFlowFinished?.Invoke();
    }


    private IEnumerator PerformPerformer(GameAction action)
    {
        Type type = action.GetType();
        if (performers.ContainsKey(type))
        { 
            yield return performers[type](action);
        }
        
    }


    private void PerformSubscribers(GameAction action, Dictionary<Type, List<Action<GameAction>>> subs)
    {
        Type type = action.GetType();
        if (subs.ContainsKey(type))
        {
            foreach (var sub in subs[type])
            {
                sub(action);
            }
        }
    }


    private IEnumerator PerformReactions()
    {
        foreach (var reaction in reactions)
        {
            yield return Flow(reaction);
        }
    }


    /// <summary>
    /// 给动作类型绑定执行逻辑（AttachPerformer）
    /// </summary>
    public static void AttachPerformer<T>(Func<T, IEnumerator> performer) where T : GameAction
    {
        Type type = typeof(T);
        IEnumerator wrappedPerformer(GameAction action) => performer((T)action);
        if (performers.ContainsKey(type)) performers[type] = wrappedPerformer;
        else performers.Add(type, wrappedPerformer);
    }

    public static void DetachPerformer<T>() where T : GameAction
    {
        Type type = typeof(T);
        if (performers.ContainsKey(type))
            performers.Remove(type);
    }


    // ========== 订阅反应（修复原版取消订阅bug版本） ==========
    // 原版bug：订阅和取消订阅每次都新建wrappedReaction，委托引用不一样，Remove永远删不掉
    // 修复方案：把包装后的委托存起来，取消订阅时用同一个委托对象移除
    public class SubscriptionWrapper<T> where T : GameAction
    {
        public Action<GameAction> Wrapped;
        public Action<T> Original;
        public SubscriptionWrapper(Action<T> reaction)
        {
            Original = reaction;
            Wrapped = (act) => reaction((T)act);
        }
    }

    public static void SubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
    {
        Dictionary<Type, List<Action<GameAction>>> subs = timing == ReactionTiming.PRE ? preSubs : postSubs;
        var wrapper = new SubscriptionWrapper<T>(reaction);
        Type type = typeof(T);
        if (subs.ContainsKey(type))
        {
            subs[type].Add(wrapper.Wrapped);
        }
        else
        {
            subs.Add(type, new List<Action<GameAction>>());
            subs[type].Add(wrapper.Wrapped);
        }
    }

    public static void UnsubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
    {
        Dictionary<Type, List<Action<GameAction>>> subs = timing == ReactionTiming.PRE ? preSubs : postSubs;
        if (subs.ContainsKey(typeof(T)))
        {
            void wrappedReaction(GameAction action) => reaction((T)action);
            subs[typeof(T)].Remove(wrappedReaction);
        }
    }


}

