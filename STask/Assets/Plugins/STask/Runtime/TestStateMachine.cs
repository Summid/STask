using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SFramework.Threading.Tasks
{
    public class TestStateMachine : MonoBehaviour
    {
        MyStateMachine stateMachine;

        private void Start()
        {
            this.stateMachine = new MyStateMachine() { state = -1, builder = new MyBuilder() };
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                this.stateMachine.MoveNext();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                //Debug.Log($"continuation's target {((MyStateMachine)this.stateMachine.awaiter.continuation.Target).awaiter.num}");
                this.stateMachine.awaiter.continuation?.Invoke();
            }
        }
    }

    public struct MyStateMachine
    {
        public int state;
        public MyBuilder builder;
        public MyAwaiter awaiter;

        public void MoveNext()
        {
            if (this.state == -1)
            {
                this.state = 0;
                this.awaiter = new MyAwaiter() { num = 123, str = "123" };
                this.builder.AwaitUnsafeOnCompleted(ref this.awaiter, ref this);
                //return;
            }
            else if (this.state == 0)
            {
                this.state = 1;
                this.awaiter.GetResult();
            }

            Debug.Log($"num:{this.awaiter.num} | str:{this.awaiter.str}");
        }
    }

    public class MyBuilder
    {
        public void AwaitUnsafeOnCompleted(ref MyAwaiter awaiter, ref MyStateMachine myStateMachine)
        {
            awaiter.UnsafeOnCompleted(myStateMachine.MoveNext); //这里会拷贝一份myStateMachine到计算堆栈的顶部（Ldobj命令，将地址指向的值类型对象复制到计算堆栈的顶部）
            //awaiter.UnsafeOnCompleted(myStateMachine.state);
            Debug.Log($"MB {awaiter.GetHashCode()}");
        }
    }

    public struct MyAwaiter
    {
        public int num;
        public string str;
        public Action continuation;

        public void GetResult()
        {
            Debug.Log("GetResult");
            //Debug.Log($"GetResult {((MyStateMachine)continuation.Target).awaiter.GetHashCode()}");
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            this.num = 233;
            this.str = "233";
            this.continuation = continuation;
            Debug.Log($"MyAwaiter {((MyStateMachine)(continuation.Target)).awaiter.GetHashCode()}");
        }

        public void UnsafeOnCompleted(int i)
        {

        }
    }
}
//为何执行到 MyAwaiter.GetResult() 后，其成员变量都为在 MyStateMachine 中初始化时候的值？
//当 MyStateMachine 为 struct （release模式下），将要执行 MyBuilder.AwaitUnsafeOnCompleted 中的 await.UnsafeOnCompleted 时
//通过ildasm工具可以看到il代码先将 myStateMachine 拷贝了一份，再执行的方法

//为何要先拷贝再调用？
//由于 awaiter.UnsafeOnCompleted 参数为 MyStateMachine 中的方法，因此需要先将 MyStateMachine 中的上下文都“捕获”起来（闭包）
//若MyStateMachine为class则不存在问题，很迷惑

//由于 MyAwaiter 为 struct 类型，在拷贝 MyStateMachine 时也就 new 了一个新的 MyAwaiter
//当 MyAwaiter 为 class 类型时，虽然也会拷贝 MyStateMachine，但也不存在上述问题