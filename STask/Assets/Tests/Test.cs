using SFramework.Threading.Tasks.Internal;
using System.Collections;
using System.Collections.Generic;
using SFramework.Threading.Tasks;
using UnityEngine;

public class Test : MonoBehaviour
{
    private void Start()
    {
    }

    public class TestTaskPoolNode : ITaskPoolNode<TestTaskPoolNode>
    {
        private static TaskPool<TestTaskPoolNode> pool;

        private TestTaskPoolNode nextNode;
        public ref TestTaskPoolNode NextNode => ref this.nextNode;

        static TestTaskPoolNode()
        {
            TaskPool.RegisterSizeGetter(typeof(TestTaskPoolNode), () => pool.Size);
        }

        public static TestTaskPoolNode Create()
        {
            if (pool.TryPop(out var node))
            {
                node = new TestTaskPoolNode();
            }
            return node;
        }

        public bool Dispose()
        {
            return pool.TryPush(this);
        }
    }
    
    #region 测试MinimumQueue
    //private MinimumQueue<int> minimumQueue = new MinimumQueue<int>(0);
    //private int version = 0;
    //private Vector2 scrollPos = Vector2.zero;
    //private void OnGUI()
    //{
    //    GUILayout.BeginHorizontal();
    //    {
    //        if (GUI.Button(GUILayoutUtility.GetRect(100, 50), "入队"))
    //        {
    //            this.minimumQueue.Enqueue(version++);
    //        }
    //        if (GUI.Button(GUILayoutUtility.GetRect(100, 50), "出队"))
    //        {
    //            this.minimumQueue.Dequeue();
    //        }
    //    }
    //    GUILayout.EndHorizontal();

    //    this.scrollPos = GUILayout.BeginScrollView(this.scrollPos);
    //    {
    //        foreach (var item in minimumQueue.array)
    //        {
    //            GUILayout.Label(new GUIContent(item.ToString()));
    //        }
    //    }
    //    GUILayout.EndScrollView();
    //}
    #endregion
}
