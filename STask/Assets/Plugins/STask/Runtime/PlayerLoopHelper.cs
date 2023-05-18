using SFramework.Threading.Tasks.Internal;
using System;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using PlayerLoopType = UnityEngine.PlayerLoop;

namespace SFramework.Threading.Tasks
{
    public static class STaskLoopRunners
    {
        public struct STaskLoopRunnerInitialization { };
        public struct STaskLoopRunnerEarlyUpdate { };
        public struct STaskLoopRunnerFixedUpdate { };
        public struct STaskLoopRunnerPreUpdate { };
        public struct STaskLoopRunnerUpdate { };
        public struct STaskLoopRunnerPreLateUpdate { };
        public struct STaskLoopRunnerPostLateUpdate { };

        // Last

        public struct STaskLoopRunnerLastInitialization { };
        public struct STaskLoopRunnerLastEarlyUpdate { };
        public struct STaskLoopRunnerLastFixedUpdate { };
        public struct STaskLoopRunnerLastPreUpdate { };
        public struct STaskLoopRunnerLastUpdate { };
        public struct STaskLoopRunnerLastPreLateUpdate { };
        public struct STaskLoopRunnerLastPostLateUpdate { };

        // Yield

        public struct STaskLoopRunnerYieldInitialization { };
        public struct STaskLoopRunnerYieldEarlyUpdate { };
        public struct STaskLoopRunnerYieldFixedUpdate { };
        public struct STaskLoopRunnerYieldPreUpdate { };
        public struct STaskLoopRunnerYieldUpdate { };
        public struct STaskLoopRunnerYieldPreLateUpdate { };
        public struct STaskLoopRunnerYieldPostLateUpdate { };

        // Yield Last

        public struct STaskLoopRunnerLastYieldInitialization { };
        public struct STaskLoopRunnerLastYieldEarlyUpdate { };
        public struct STaskLoopRunnerLastYieldFixedUpdate { };
        public struct STaskLoopRunnerLastYieldPreUpdate { };
        public struct STaskLoopRunnerLastYieldUpdate { };
        public struct STaskLoopRunnerLastYieldPreLateUpdate { };
        public struct STaskLoopRunnerLastYieldPostLateUpdate { };

        //需要 Unity 2020.2 以及更新版本支持
        public struct STaskLoopRunnerTimeUpdate { };
        public struct STaskLoopRunnerLastTimeUpdate { };
        public struct STaskLoopRunnerYieldTimeUpdate { };
        public struct STaskLoopRunnerLastYieldTimeUpdate { };
    }

    [Flags]
    public enum InjectPlayerLoopTimings
    {
        /// <summary>
        /// 预设: All loops(default).
        /// </summary>
        All =
            Initialization | LastInitialization |
            EarlyUpdate | LastEarlyUpdate |
            FixedUpdate | LastFixedUpdate |
            PreUpdate | LastPreUpdate |
            Update | LastUpdate |
            PreLateUpdate | LastPreLateUpdate |
            PostLateUpdate | LastPostLateUpdate
            //需要 Unity 2020.2 以及更新版本支持
            | TimeUpdate | LastTimeUpdate,


        /// <summary>
        /// 预设: 排除 LastPostLateUpdate
        /// </summary>
        Standard =
            Initialization |
            EarlyUpdate |
            FixedUpdate |
            PreUpdate |
            Update |
            PreLateUpdate |
            PostLateUpdate | LastPostLateUpdate
        //需要 Unity 2020.2 以及更新版本支持
            | TimeUpdate,

        /// <summary>
        /// 预设: 最小配置, Update | FixedUpdate | LastPostLateUpdate
        /// </summary>
        Minimum =
            Update | FixedUpdate | LastPostLateUpdate,


        // PlayerLoopTiming

        Initialization = 1,
        LastInitialization = 2,

        EarlyUpdate = 4,
        LastEarlyUpdate = 8,

        FixedUpdate = 16,
        LastFixedUpdate = 32,

        PreUpdate = 64,
        LastPreUpdate = 128,

        Update = 256,
        LastUpdate = 512,

        PreLateUpdate = 1024,
        LastPreLateUpdate = 2048,

        PostLateUpdate = 4096,
        LastPostLateUpdate = 8192,
        //需要 Unity 2020.2 以及更新版本支持
        // Unity 2020.2 added TimeUpdate https://docs.unity3d.com/2020.2/Documentation/ScriptReference/PlayerLoop.TimeUpdate.html
        TimeUpdate = 16384,
        LastTimeUpdate = 32768
    }

    public static class PlayerLoopHelper
    {
        private static int mainThreadId;
        private static SynchronizationContext unitySynchronizationContext;
        private static ContinuationQueue[] yielders;
        private static PlayerLoopRunner[] runners;

        public static int MainThreadId => mainThreadId;
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;
        public static SynchronizationContext UnitySynchronizationContext => unitySynchronizationContext;

        private static PlayerLoopSystem[] InsertRunner(PlayerLoopSystem loopSystem,bool injectOnFirst,
            Type loopRunnerYieldType,ContinuationQueue cq,
            Type loopRunnerType,PlayerLoopRunner runner)
        {
#if UNITY_EDITOR
            //进入Play模式前后清除迭代对象，清除之前再跑一次
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
                {
                    if (runner != null)
                    {
                        runner.Run();
                        runner.Clear();
                    }
                    if (cq != null)
                    {
                        cq.Run();
                        cq.Clear();
                    }
                }
            };
#endif

            PlayerLoopSystem yieldLoop = new PlayerLoopSystem
            {
                type = loopRunnerYieldType,
                updateDelegate = cq.Run
            };

            PlayerLoopSystem runnerLoop = new PlayerLoopSystem
            {
                type = loopRunnerType,
                updateDelegate = runner.Run
            };

            //若重复添加subLoopSystem，则移除之前的
            PlayerLoopSystem[] source = RemoveRunner(loopSystem, loopRunnerYieldType, loopRunnerType);
            PlayerLoopSystem[] dest = new PlayerLoopSystem[source.Length + 2];

            Array.Copy(source, 0, dest, injectOnFirst ? 2 : 0, source.Length);
            if (injectOnFirst)
            {
                dest[0] = yieldLoop;
                dest[1] = runnerLoop;
            }
            else
            {
                dest[dest.Length - 2] = yieldLoop;
                dest[dest.Length - 1] = runnerLoop;
            }

            return dest;
        }

        /// <summary>
        /// 删除PlayerLoopSystem中指定的 yieldType 和 runnerType 并返回该PlayerLoopSystem新的 subSystemList
        /// </summary>
        /// <param name="loopSystem"></param>
        /// <param name="loopRunnerYieldType"></param>
        /// <param name="loopRunnerType"></param>
        /// <returns></returns>
        private static PlayerLoopSystem[] RemoveRunner(PlayerLoopSystem loopSystem,Type loopRunnerYieldType,Type loopRunnerType)
        {
            return loopSystem.subSystemList
                .Where(ls => ls.type != loopRunnerYieldType && ls.type != loopRunnerType)
                .ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            //捕获 unity 的同步上下文
            unitySynchronizationContext = SynchronizationContext.Current;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

#if UNITY_EDITOR
            //解决编辑器关闭域重载的问题，需要 2019.3 以上的版本
            //当域重载关闭后，进入 play 前需要重新重新初始化，否则之前的 tasks 会停留在内存中导致内存泄漏
            bool domainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload);
            if (!domainReloadDisabled && runners != null)//没有关闭域重载，就当无事发生
                return;
#endif
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            Initialize(ref playerLoop);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitOnEditor()
        {
            //play 后，执行初始方法
            Init();

            //注册 Editor 的 update 生命周期方法，用于迭代 playerLoop
            EditorApplication.update += ForceEditorPlayerLoopUpdate;
        }

        private static void ForceEditorPlayerLoopUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                //没处在编辑模式，返回
                return;
            }

            if (yielders != null)
            {
                foreach (ContinuationQueue item in yielders)
                {
                    if (item != null)
                    {
                        item.Run();
                    }
                }
            }

            if (runners != null)
            {
                foreach (PlayerLoopRunner item in runners)
                {
                    if (item != null)
                    {
                        item.Run();
                    }
                }
            }
        }
#endif

        private static int FindLoopSystemIndex(PlayerLoopSystem[] playerLoopList,Type systemType)
        {
            for (int i = 0; i < playerLoopList.Length; ++i)
            {
                if (playerLoopList[i].type == systemType)
                {
                    return i;
                }
            }

            throw new Exception("PlayerLoopSystem 未找到，type:" + systemType.FullName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="copyList"></param>
        /// <param name="injectTimings">为了保持<see cref="Initialize"/>方法的通用性，无论它的injectTimings参数是All还是Standard，它都会申请注入所有InjectPlayerLoopTimings类型（All、Standard、Minimum除外）
        /// 而判断是否注入则由本方法完成</param>
        /// <param name="loopType"></param>
        /// <param name="targetTimings"></param>
        /// <param name="index"></param>
        /// <param name="injectOnFirst"></param>
        /// <param name="loopRunnerYieldType"></param>
        /// <param name="loopRunnerType"></param>
        /// <param name="playerLoopTiming"></param>
        private static void InsertLoop(PlayerLoopSystem[] copyList, InjectPlayerLoopTimings injectTimings, Type loopType, InjectPlayerLoopTimings targetTimings,
            int index, bool injectOnFirst, Type loopRunnerYieldType, Type loopRunnerType, PlayerLoopTiming playerLoopTiming)
        {
            int i = FindLoopSystemIndex(copyList, loopType);
            if ((injectTimings & targetTimings) == targetTimings)//符合条件才注入
            {
                copyList[i].subSystemList = InsertRunner(copyList[i], injectOnFirst,
                    loopRunnerYieldType, yielders[index] = new ContinuationQueue(playerLoopTiming),
                    loopRunnerType, runners[index] = new PlayerLoopRunner(playerLoopTiming));
            }
            else
            {
                //不符合则移除
                copyList[i].subSystemList = RemoveRunner(copyList[i], loopRunnerYieldType, loopRunnerType);
            }
        }

        public static void Initialize(ref PlayerLoopSystem playerLoop, InjectPlayerLoopTimings injectTimings = InjectPlayerLoopTimings.All)
        {
            yielders = new ContinuationQueue[16];
            runners = new PlayerLoopRunner[16];

            PlayerLoopSystem[] copyList = playerLoop.subSystemList.ToArray();

            // Initialization
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.Initialization),
                InjectPlayerLoopTimings.Initialization, 0, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldInitialization), typeof(STaskLoopRunners.STaskLoopRunnerInitialization), PlayerLoopTiming.Initialization);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.Initialization),
                InjectPlayerLoopTimings.LastInitialization, 1, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldInitialization), typeof(STaskLoopRunners.STaskLoopRunnerLastInitialization), PlayerLoopTiming.LastInitialization);

            // EarlyUpdate
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.EarlyUpdate),
                InjectPlayerLoopTimings.EarlyUpdate, 2, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldEarlyUpdate), typeof(STaskLoopRunners.STaskLoopRunnerEarlyUpdate), PlayerLoopTiming.EarlyUpdate);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.EarlyUpdate),
                InjectPlayerLoopTimings.LastEarlyUpdate, 3, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldEarlyUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastEarlyUpdate), PlayerLoopTiming.LastEarlyUpdate);

            // FixedUpdate
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.FixedUpdate),
                InjectPlayerLoopTimings.FixedUpdate, 4, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldFixedUpdate), typeof(STaskLoopRunners.STaskLoopRunnerFixedUpdate), PlayerLoopTiming.FixedUpdate);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.FixedUpdate),
                InjectPlayerLoopTimings.LastFixedUpdate, 5, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldFixedUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastFixedUpdate), PlayerLoopTiming.LastFixedUpdate);

            // PreUpdate
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.PreUpdate),
                InjectPlayerLoopTimings.PreUpdate, 6, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldPreUpdate), typeof(STaskLoopRunners.STaskLoopRunnerPreUpdate), PlayerLoopTiming.PreUpdate);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.PreUpdate),
                InjectPlayerLoopTimings.LastPreUpdate, 7, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldPreUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastPreUpdate), PlayerLoopTiming.LastPreUpdate);

            // Update
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.Update),
                InjectPlayerLoopTimings.Update, 8, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldUpdate), typeof(STaskLoopRunners.STaskLoopRunnerUpdate), PlayerLoopTiming.Update);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.Update),
                InjectPlayerLoopTimings.LastUpdate, 9, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastUpdate), PlayerLoopTiming.LastUpdate);

            // PreLateUpdate
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.PreLateUpdate),
                InjectPlayerLoopTimings.PreLateUpdate, 10, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldPreLateUpdate), typeof(STaskLoopRunners.STaskLoopRunnerPreLateUpdate), PlayerLoopTiming.PreLateUpdate);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.PreLateUpdate),
                InjectPlayerLoopTimings.LastPreLateUpdate, 11, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldPreLateUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastPreLateUpdate), PlayerLoopTiming.LastPreLateUpdate);

            // PostLateUpdate
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.PostLateUpdate),
                InjectPlayerLoopTimings.PostLateUpdate, 12, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldPostLateUpdate), typeof(STaskLoopRunners.STaskLoopRunnerPostLateUpdate), PlayerLoopTiming.PostLateUpdate);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.PostLateUpdate),
                InjectPlayerLoopTimings.LastPostLateUpdate, 13, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldPostLateUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastPostLateUpdate), PlayerLoopTiming.LastPostLateUpdate);

            //UNITY_2020_2_OR_NEWER
            // TimeUpdate
            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.TimeUpdate),
                InjectPlayerLoopTimings.TimeUpdate, 14, true,
                typeof(STaskLoopRunners.STaskLoopRunnerYieldTimeUpdate), typeof(STaskLoopRunners.STaskLoopRunnerTimeUpdate), PlayerLoopTiming.TimeUpdate);

            InsertLoop(copyList, injectTimings, typeof(PlayerLoopType.TimeUpdate),
                InjectPlayerLoopTimings.LastTimeUpdate, 15, false,
                typeof(STaskLoopRunners.STaskLoopRunnerLastYieldTimeUpdate), typeof(STaskLoopRunners.STaskLoopRunnerLastTimeUpdate), PlayerLoopTiming.LastTimeUpdate);

            playerLoop.subSystemList = copyList;
            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        public static void AddAction(PlayerLoopTiming timing, IPlayerLoopItem action)
        {
            PlayerLoopRunner runner = runners[(int)timing];
            if (runner == null)
            {
                ThrowInvalidLoopTiming(timing);
            }
            runner.AddAction(action);
        }

        public static void AddContinuation(PlayerLoopTiming timing,Action continuation)
        {
            ContinuationQueue queue = yielders[(int)timing];
            if (queue == null)
            {
                ThrowInvalidLoopTiming(timing);
            }
            queue.Enqueue(continuation);
        }

        private static void ThrowInvalidLoopTiming(PlayerLoopTiming playerLoopTiming)
        {
            throw new InvalidOperationException("playerLoopTiming 未注入；请检查 PlayerLoopHelper.Initialize 方法；PlayerLoopTiming:" + playerLoopTiming);
        }
    }
}