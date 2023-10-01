# STask

STask 是以学习 UniTask 而建立的教程仓库，[原文地址](https://summid.icu/index.php/2023/10/01/stasktutorials1/)。



# Unity异步扩展实践（一）——以`UniTask`库为参考

## 背景

### What?

异步方法诞生于C# 5时代，它基于 `Task` 和 `Task<T>` 类型，让C#在异步编程领域的思想产生了巨大转变。以从文件中读取内容为例：

```c#
static async Task Main(string[] args)
{
    string fileName = "d:/1.txt";
    string s = await File.ReadAllTextAsync(fileName);
    Console.WriteLine(s);
}
```

上面的代码将**等待**读取完文件中的内容后，将其输出到控制台窗口中。C#中对于文件操作的方法几乎都有与之对应的异步版本。在异步方法出现之前，要实现异步操作的功能，需要程序员手动开启新线程，并将任务分配给线程，然后监听线程完成工作的情况，待任务结束后还需关闭线程。如此复杂的步骤无疑给程序员增加了工作负担——为了获得更好性能，我们同时付出了复杂编码的代价。

而异步方法出现之后，其将编码简化到了仅靠两个关键字（`async`、`await`）和 `Task` 对象就能以同步编程的方式实现异步的效果。

异步方法的实现原理其实很简单——在程序编译前，编译器会将异步方法转换为状态机，当 `await` 语句后面的任务没有完成时，状态机将执行到这一句并等待，而在等待的过程中，线程将去执行其他任务，不会一直阻塞在这里。一旦任务完成，状态机被推动到下一个状态，线程继续执行 `await` 语句到之后的代码。

使用异步方法时，程序员对其中的线程切换几乎没有任何感知。**在 `.NET` 实现中，执行 `await` 语句之前和之后代码的线程是有可能不同的。**虽然有线程切换，但我们不需要关心其中的细节，`.NET` 已经帮我们完成了其中的脏活累活，让结果看起来和单线程一致。

可以将 `Task` 类型看作对多线程的包装，将异步方法看作对 `Task` 类型的扩展。异步方法的 `async` 和 `await` 关键字本质上是C#提供的语法糖。

### Why?

异步方法的语法如此优美，运行原理也科学有效，我们为何还需要基于C#的异步机制，重新造一个轮子呢？

当某项简单易用的新（yu）技（fa）术（tang）诞生后，它很容易在项目中被“滥用”，并且用户会忽略其背后的代价。在使用异步方法时，会有隐形的申请 `Task` 对象内存的性能消耗——虽然我们没有直接申请它，但异步方法状态机却这么做了。

后来 `.NET` 推出了 `ValueTask` 类型，解决了当异步方法以同步的方式完成时，不必要的内存开销。但这足够好了吗？

我们知道Unity是单线程的，任何跟引擎相关的操作都必须在主线程中进行，而异步方法内部是存在线程切换的，这就是问题所在。虽然Unity的 `.NET` 实现库会确保 `await` 语句后面的代码仍然会交由主线程执行，但其背后仍然有针对“同步上下文”所进行的不必要的操作，这也是额外的性能开销。

除了性能问题，异步方法能带给我们的不只是等待资源加载，等待网络请求返回这些便捷，我们甚至可以编写自己的可等待对象，一行代码实现等待N秒、N帧的效果，彻底摆脱协程，同时做到 0 GC。

```c#
AssetBundle ab = await AssetBundle.LoadFromFileAsync(path);//等待资源加载

var txt = (await UnityWebRequest.Get("https://...").SendWebRequest()).downloadHandler.text;//等待网络请求

await UniTask.Delay(TimeSpan.FromSeconds(10));//等待十秒

await UniTask.NextFrame();//等待到下一帧
```

### How?

好消息是，我们已经有了屌炸天的 `UniTask` 库，你能想到的，它已经帮你实现好了，暂时没想到的，它也提前做好了，并且它免费开源：

[github地址](https://github.com/Cysharp/UniTask) 

想要快速了解和上手的同学，可以阅读仓库中的文档，支持中文。

对于想要深入学习 `UniTask` 源码的小伙伴，打开项目后可能会被其中大量的接口、扩展方法、工具类给弄得一脸懵逼。但经过笔者的大致研究之后，发现 `UniTask` 库对于刚接触C#编程的小伙伴是非常具有研究价值的，因此这篇文章将以 `UniTask` 为例，从原理到代码分享笔者的 Unity 异步库实现的经验。

**注意嗷：**由于 `UniTask` 库中有许多非核心的附加代码，比如为了兼容不同版本的C#、Unity，以及编辑器相关的代码。这些代码对实现异步库的核心功能没有实质上的作用，后文的代码部分会将这部分给去除掉，方便大伙儿理解。因此为了跟 `UniTask` 区分，后面我们将异步库命名为 `STask` ，S 代表着 Simple。



## 原理

想要自己实现一套基于关键字 `async` 、 `await` 的高效异步机制，我们需要有自己的：

- Task Type，即可以被 `await` 等待的，类似 `Task` 的类型。
- Builder Type，该类型包含的成员，可以看作是状态机对外提供的接口。

那么，我们应该怎么实现这俩类型呢？其实微软已经有文档帮我们总结了步骤：[文档跳转](https://github.com/dotnet/roslyn/blob/main/docs/features/task-types.md) 。

下面我们简单说明下该文档的重点。

### Task Type

1. 自定义 `task` 可以是 `class` 或 `struct` 类型。后续实践我们将采用 `struct` 。
2. 自定义 `task` 需要用特性 `System.Runtime.CompilerServices.AsyncMethodBuilderAttribute` 标记，来与异步状态机建立关系。
3. 自定义 `task` 可以有泛型参数，该参数用于异步方法的返回值类型；也可以没有泛型参数。
4. 自定义 `task` 需要定义有 `GetAwaiter()` 方法（扩展方法也可以），有了这个方法，其可以被 `await` 关键字“等待”。

根据上面的需求，我们的自定义 `task` 会如同下方的实例代码：

```c#
[AsyncMethodBuilder(typeof(MyTaskMethodBuilder<>))]
class MyTask<T>
{
    public Awaiter<T> GetAwaiter();
}

//除了自定义 task 之外，我们还需定义其使用的 Awaiter 类型
class Awaiter<T> : INotifyCompletion
{
    public bool IsCompleted { get; } //等待前，状态机将通过该变量判断 task 是否已经完成工作
    public T GetResult(); //task完成工作后，状态机通过该方法获取结果
    public void OnCompleted(Action completion); //当需要等待时，状态机调用该方法注册回调，即await关键字之后的代码
}
```

关于 `Awaiter` 类型，除了其实现接口的 `OnCompleted` 方法之外，`IsCompleted` 和 `GetResult` 这俩成员是**不可省略**的，在编译期间编译器会检查，若出现缺省的情况会导致编译不通过哦。我们会在后面讨论其工作细节。

### Builder Type

接着我们来看 `builder` 有哪些知识点：

1. `builder` 可以为 `class` 或 `struct` 类型。在后面的实践中，我们的 `builder` 为 `struct` 类型。

2. `builder` 可以有至多一个泛型参数，该参数可用作异步方法的返回值类型，并且自身不能作为泛型类型。

3. `builder` 需要定义以下访问级别为 `public` 的方法：

   ```c#
   class MyTaskMethodBuilder<T>
   {    
       public static MyTaskMethodBuilder<T> Create(); //static
   
       public void Start<TStateMachine>(ref TStateMachine stateMachine)
           where TStateMachine : IAsyncStateMachine;
       
       public void SetStateMachine(IAsyncStateMachine stateMachine);
       public void SetException(Exception exception);
       public void SetResult(T result); //若是无泛型状态机，该方法也没有参数
   
       public void AwaitOnCompleted<TAwaiter, TStateMachine>(
           ref TAwaiter awaiter, ref TStateMachine stateMachine)
           where TAwaiter : INotifyCompletion
           where TStateMachine : IAsyncStateMachine;
       public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
           ref TAwaiter awaiter, ref TStateMachine stateMachine)
           where TAwaiter : ICriticalNotifyCompletion
           where TStateMachine : IAsyncStateMachine;
   
       public MyTask<T> Task { get; } //task-like对象
   }
   ```

   下面我们来讨论这些方法的工作流程。

### 状态机的工作流程

为了让我们更直观地理解异步方法的工作机制，软件工程之神降下它的荣光，一张流程图出现在眼前：

![AsyncSequenceDiagram](D:\SaveData\OneDrive\markdown\STask\images\AsyncSequenceDiagram.png)

这张流程图可以让我们对异步机制有一个大概印象，目前我们不用将里面的每一步都理解透彻。在后面的章节针对其中细节进行讨论时，可以随时回来复习。话虽如此，但我们还是得简单讲解下流程：

1. `FuncAAsync` 是一个异步方法，其方法体包含至少一个 `await` 语句。当编译器识别到异步方法后，会在异步方法内生成一个状态机，即这里的 `GeneratedStateMachine` 。
2. 状态机生成后，与之同时 `MyBuilder` 也被创建出来。前面说过 `builder type` 可以看作是状态机开放给我们的接口，我们能通过它实现状态机的部分逻辑，因此它其实也是状态机的一部分。`MyBuilder` 被创建后，需要调用 `Start` 方法来启动状态机。
3. `MyBuilder` 的 `Start` 方法有一个接受状态机实例的参数，并且状态机内部实现了 `MoveNext` 方法，用于推动状态机的运行。我们就在 `Start` 方法中调用 `MoveNext` 来启动状态机。到这一步，我们的代码将会执行到第一个 `await` 语句之前。
4. 状态机遇到了 `await` 语句，被等待的是 `FuncBAsync` 异步方法，该方法的返回值类型是 `MyTask` 。
5. 状态机访问 `builder` 中，变量名为 `Task` 的成员，获取到 `MyTask` 类型。之后调用 `MyTask.GetAwaiter` 方法，并访问结果中的 `IsCompleted` 成员来判断等待任务是否完成。若已经完成，状态机将直接访问 `MyTaskAwaiter.GetResult` 来获取异步方法的执行结果（未在图中画出），`FuncAAsync` 将以同步的方式执行（假设只有一个 `await` 语句）。
6. 如果 `IsCompleted` 的返回结果为未完成，状态机将执行 `builder.AwaitUnsafeOnCompleted` 方法，用于注册等待任务结束后的“回调方法（其实就是状态机的 `MoveNext` 方法）”，即 `await` 语句之后的代码。
7. 目前为止，我们还未讨论过自定义异步机制中需要我们实现的具体的逻辑——只有 `builder` 中方法的定义肯定是不够的，我们要如何实现方法的功能？由此可见我们还需要一个打工仔来帮我们干活，所以我们引入了 `MyMoveNextRunner` 。我们会将第六步中的“回调方法”传到 `MyMoveNextRunner` 中进行包装。
8. “回调方法”包装完成后，我们通过调用 `UnsafeOnCompleted(action)` 方法，将它传给 `MyTaskAwaiter` 。这样，`MyTaskAwaiter` 就拿到了 `await` 语句后面的代码，当它结束等待后，知道接下来该执行什么。
9. 当 `MyTaskAwaiter` 结束等待，它将调用状态机的 `MoveNext` 方法来推动状态机运行。此时状态机将继续执行 `await` 后面的代码，直到遇到第二个 `await` （如果有），或者将 `FuncAAsync` 执行完。



第六到第九步是最重要的，而其中第六到第八步执行得十分紧密，这也是我们后续工作的重点。读者可能会有疑问，为何要在 `MyMoveNextRunner` 中包装回调？这是为了方便后续拓展，而将 `MyMoveNextRunner` 抽象成了接口，这里我们先不展开，后续会慢慢讲。



这一章我们简单介绍了为什么要定制Unity异步机制，并总结了异步状态机的工作流程。下一章我们将着手编写代码，从零开始实现异步库。



## 扩展阅读

### ValueTask

`ValueTask` 于 .NET Core 2.0 推出，它本意是为了解决当异步方法以同步方式完成时，多余的内存开销问题。它的核心思想除了将原本的 `Task` 从“引用类型”更改为“值类型”之外，还做了一些工作让 `ValueTask` 能够重复使用，当然这也导致在使用 `ValueTask` 时会有一些限制，比如不能多次 `await` 同一个 `ValueTask` 对象，因为第一次等待之后，它可能就被回收到对象池中了；再比如在多个线程中等待同一个 `ValueTask` ;以及在 `ValueTask` 完成任务之前使用 `.GetAwaiter().GetResult()` 来使异步方法以同步方式运行，这些都是不支持的。

当然在日常开发中，我们基本上只会 `await` 一个异步方法，然后使用它返回的结果，并且该异步等待没有额外的性能开销，这恰好是 `ValueTask` 的优势所在。后续的异步库开发，我们也将参考 `ValueTask` 的实现原理（实际上 `UniTask` 就是这样做的）。

更多关于 `ValueTask` 的小知识，比如回收使用的原理、其接口 `IValueTaskSource` 的抽象，小伙伴们可以参考以下两篇博客：

[Understanding the Whys, Whats, and Whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)

[Prefer ValueTask to Task, always; and don't await twice](https://blog.marcgravell.com/2019/08/prefer-valuetask-to-task-always-and.html)



### ICriticalNotifyCompletion 与 INotifyCompletion

看过 `UniTask` 源码，或者自己动手实现过 `task-like` 类型的小伙伴可能会遇到这两个接口，并对它们的行为产生疑惑——它们的作用貌似没有什么区别，那是出于什么考虑要分成两个接口？

我们可以简单记住：当实现自己的 `awaiter` 时，尽量实现 `ICriticalNotifyCompletion` 接口，状态机将优先调用该接口的方法。而 `INotifyCompletion` 接口，则更像是一个“历史遗留问题”，但同时实现两个接口也是没有问题的。至于什么时候只实现 `INotifyCompletion` ，论坛中有关于此的讨论：

[What is ICriticalNotifyCompletion for?](https://stackoverflow.com/questions/65529509/what-is-icriticalnotifycompletion-for)



# Unity异步扩展实践（二）——以UniTask库为参考

## 总览

正式编写代码前，我们需要对异步框架有一个大体的认识，因此我简单画了一幅类图，帮助大家留个印象：

## ![STaskMain](D:\SaveData\OneDrive\markdown\STask\images\STaskMain.png)

除了接口和类 `AsyncSTask` 、`TaskPool` 以外，大部分结构都是结构体struct类型，同时我也在图中特别注明了，`TaskPool` 为静态类。

通过图我们得知，状态机接口 `AsyncSTaskMethodBuilder` 通过接口 `IStateMachineRunnerPromise` 来与处理异步逻辑的打工仔 `AsyncSTask` 建立连接，而真正的异步核心机制则交给 `STaskCompletionSourceCore` 来完成。由于 `AsyncSTask` 实现的接口比较多，为了更清楚地浏览，我将它的成员进行分类展示。状态机每次等待未完成的 `STask` 时，都会创建一个 `AsyncSTask` 对象（创建过程后续会讨论），因此为了性能考虑，我们需要将它“池化”——引入对象池 `TaskPool` 。

经过第一章的介绍，大伙儿应该不会对另一边的 `STask` 和 `Awaiter` 太陌生了。`STask` 需要实现 `GetAwaiter()` 方法，因此他俩天生就是一对。而 `STask` 和 `AsyncSTask` 的关系则是靠接口 `ISTaskSource` 建立起来的。

后面我们首先会讨论对象池 `TaskPool` 的设计与实现，因为在整个框架中，它相对独立，原理也简单。随后，我们将讨论其他模块的细节。我会优先介绍比较重要的部分，如 `STaskCompletionSourceCore` 。这不一定是最推荐的阅读顺序，比如有的小伙伴更习惯自上而下地拆分系统进行学习，比如从 `AsyncSTaskMethodBuilder` 开始，因此读者可以根据需要，选择不同的小节开始。

注：出于篇幅考虑，我们只会分析有返回值的异步方法，在实现上它比无返回值的异步方法多了返回类型泛型，并且多了设置、获取方法结果的操作。

## TaskPool

`TaskPool` 内部可分为两部分，其中**静态类** `TaskPool` 是纯纯的工具类，提供获取对象池容量的接口，**不实现**对象池功能；另一部分 `Task<T>` 则是对象池主体，提供存取接口，出于性能考虑，我们将它定义为结构体类型。除此之外，我们还定义了接口 `ITaskPoolNode<T>` ，任何要使用对象池的对象需要实现它的属性 `NextNode` 。这一节的代码可以在 TaskPool.cs 文件中找到。

### static class TaskPool

工具类 `TaskPool` 相对比较独立，我们先来分析它：

```c#
    /// <summary>
    /// TaskPool工具类，提供获取对象池容量的接口，不实现对象池功能
    /// </summary>
    public static class TaskPool
    {
        internal static int MaxPoolSize;

        private static Dictionary<Type, Func<int>> sizes = new Dictionary<Type, Func<int>>();

        static TaskPool()
        {
            //先从环境变量中寻找预定义的大小，若没找到则默认最大值
            try
            {
                string value = Environment.GetEnvironmentVariable("STASK_MAX_POOLSIZE");
                if (value != null)
                {
                    if (int.TryParse(value, out int size))
                    {
                        MaxPoolSize = size;
                        return;
                    }
                }
            }
            catch { }

            MaxPoolSize = int.MaxValue;
        }

        public static void SetMaxPoolSize(int maxPoolSize)
        {
            MaxPoolSize = maxPoolSize;
        }

        public static IEnumerable<(Type, int)> GetCacheSizeInfo()
        {
            lock (sizes)
            {
                foreach (KeyValuePair<Type, Func<int>> item in sizes)
                {
                    yield return (item.Key, item.Value());
                } 
            }
        }

        public static void RegisterSizeGetter(Type type, Func<int> getSize)
        {
            lock (sizes)
            {
                sizes[type] = getSize;
            }
        }
    }
```

- 当 `TaskPool` 类第一次被其他人调用，会先进入静态构造方法，我们在这里设置池子的最大容量。
- 池子会优先使用环境变量中预定义的容量（如果有定义），若没有则取整型的最大值。除了通过环境变量来设置容量，还可以通过 `SetMaxPoolSize(int)` 来设置。
- 我们可通过 `TaskPool` 类来获取具体某个池子当前的大小，但获取的方法需要外部提供（一般来说就由使用池子的对象提供）。使用对象池时，我们一般会顺便调用 `RegisterSizeGetter(Type, Func<int>)` 来注册获取对象池大小的方法。
- 当我们需要查询当前的对象池们的大小时，可通过 `GetCacheSizeInfo()` 方法来实现。该方法内部由 yield return 实现，若用户需要查询某一个池子的大小，可以轻松地使用 foreach 来迭代其返回的 `IEnumerable<Type, int>` 对象，并在找到目标池子后跳出循环，不用担心出现过多的迭代与调用。这样写的好处是方法只实现最基本的功能，让工具类的代码更简洁，至于是查询所有对象池还是某一个对象池，则由调用者决定。



### interface ITaskPoolNode<T>

假设在一款射击游戏中，我们有一个子弹类 `Bullet` ，在合适的时候，我们需要将 `Bullet` 对象放进对象池。在 `TaskPool` 这一套对象池工具下，`Bullet` 需要实现 `ITaskPoolNode<T>` 接口：

```c#
public interface ITaskPoolNode<T>
{
    ref T NextNode { get; }
}

public class Bullet : ITaskPoolNode<Bullet>
{
	public ref Bullet NextNode { get; }
}
```

看到 `NextNode` 属性，对数据结构比较熟悉的小伙伴可能会联想到链表。没错，由于对象池容器需要频繁地存取对象，我们在此使用链表将其优势发扬光大。

通常，我们不会让节点对象自己持有 `NextNode` 属性，因为在C#中，我们可以直接使用双向链表 `LinkedList<T>` 。这样做十分方便，但代价是我们需要给每一种节点都开一个 `LinkedList<T>` 容器。而在游戏运行时，可能有几十上百个 task 并发运行，虽说都是 task ，但考虑到泛型，我们仍可能要维护很多个 `LinkedList<T>` ，这是我们想避免的，因此我们把访问下一个节点的功能放进节点自身。

节点的数据类型有可能是“值类型”的，比如 struct ，所以 `NextNode` 属性加了 ref 关键字。

下面我们来讨论对象池最核心逻辑的实现。



### struct TaskPool<T>

```c#
    [StructLayout(LayoutKind.Auto)]
    public struct TaskPool<T> where T : class, ITaskPoolNode<T>
    {
        private int gate;
        private int size;
        private T root;//root为链表头，出队与入队都在root位置操作；node0 <-- node1 <-- node2 <-- node3 (root), node3's NextNode is node2

        public int Size => this.size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out T result)
        {
            //if gate equals 'comparand', it will be replaced by 'value', return original value in 'location1'
            if (Interlocked.CompareExchange(ref this.gate, 1, 0) == 0)
            {
                T v = this.root;
                if (!(v is null))
                {
                    ref T nextNode = ref v.NextNode;
                    this.root = nextNode;
                    nextNode = null;
                    this.size--;
                    result = v;
                    //The 'Volatile.Write' method forces the value in 'location' to be written to at the point of the call.
                    //In addition, any earlier program-order loads and stores must occur before the call to Volatile.Write.
                    Volatile.Write(ref this.gate, 0);
                    return true;
                }

                Volatile.Write(ref this.gate, 0);
            }

            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPush(T item)
        {
            if (Interlocked.CompareExchange(ref this.gate, 1, 0) == 0)
            {
                if (this.size < TaskPool.MaxPoolSize)
                {
                    item.NextNode = this.root;
                    this.root = item;
                    this.size++;
                    Volatile.Write(ref this.gate, 0);
                    return true;
                }

                Volatile.Write(ref this.gate, 0);
            }

            return false;
        }
    }
```

重点在 `TryPop(out T)` 和 `TryPush(T)` 方法上，逻辑都比较简单，下面挑几个特别的地方说明一下：

- 链表的头指针 `root` （根据它的命名，这里笔者将其称为头指针），是存放和取出节点的位置。如果有小伙伴手搓过链表，应该听说过链表的“头插法”和“尾插法”，这里采用的前者。

- 考虑到多线程安全，在存取操作之前都会加锁，而我们要实现的异步库本身就是单线程的，因此在遇到上了锁的情况会直接返回。

- 判断有没有上锁，和上锁的操作是通过原子操作 `Interlocked.CompareExchange(int, int, int)` 来实现的。它会判断 `gate` 是否等于第三个参数 `0` ，如果相等，则将第二个参数的值 `1` 赋给 `gate` ，最终返回 `gate` 原本的值。~~一行代码就完成了这两件事，太优雅了！~~

- 最后解锁操作使用的 `Volatile.Write(int, int)` ，表示将第二个参数写进第一个参数里。为什么不直接赋值，也是考虑到多线程的问题—— `gate` 可能由多个同时执行的线程修改，出于性能考虑，编译器，运行时系统甚至硬件都可能重新排列对存储器位置的读取和写入。意思就是如果直接写入，那么这一行代码可能会被优化到其他地方，导致代码的执行顺序被修改。使用 `Volatile.Write(int, int)` 可以确保代码执行顺序不会改变，将它“固定住”。

- `TaskPool<T>` 被定义为 struct 类型，也是为性能做的让步，因此在使用时最好不要拷贝它。其实在后续的使用中，我们一般会将它定义成静态变量：

  ```c#
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
  ```

  我们会直接把对象池放在节点里，通过静态方法 `Create()` 去获取新节点，使用完后调用 `Dispose()` 放进对象池，用户在使用过程中甚至感受不到对象池的存在。



### 小结

这一节介绍了 `STask` 中的对象池，它是异步库性能优化中不可或缺环节。在 `UniTask` 中，这部分代码十分简洁明了，因此 `STask` 直接沿用了下来。对象池的实现方法有很多种，具体采用什么方法需要视具体情况。这一节所介绍的对象池，非常适合只需要最基本的缓存功能，且使用方便的场景，我们没有去遍历对象池中的对象，或者将对象池清空这些奇奇怪怪的需求，小伙伴们也可以将它应用在其他合适的地方。



## STaskCompletionSourceCore

### STaskCompletionSourceCore<TResult>

从命名来看，`STaskCompletionSourceCore` 的结尾单词 'core' ，我们就能感受到它在整个系统中起到了举足轻重的作用。这个类非常类似 `ValueTask` 所用到的 [ManualResetValueTaskSourceCore](https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Sources/ManualResetValueTaskSourceCore.cs,2ea1d69c971646b9) 。我们在上层调用的 `builder.SetResult(T)` 、 `builder.SetException(Exception)` 和 `awaiter.OnCompleted(Action)` 等方法都会最终执行到 `core.TrySetResult(TResult)` 、 `core.TrySetException(Exception)` 和 `core.OnCompleted(Action<object>, object, token)` 方法里。

这一节一共会介绍四位新朋友，除了 `STaskCompletionSourceCore` 外，还有帮忙处理异常的 `ExceptionHolder` 、为了性能而从本体中提取出来形成静态类的 `STaskCompletionSourceCoreShared` 、以及负责派发异常的 `STaskScheduler` 。我们会先讨论前三者，因为它们的耦合度比较高，可以看作一个整体，之后我们再来讨论 `STaskScheduler`。

下面的代码编写在 `STaskCompletionSource.cs` 文件中。

```c#
	[StructLayout(LayoutKind.Auto)]
    public struct STaskCompletionSourceCore<TResult>
    {
        //Struct Size: TResult + (8 + 2 + 1 + 1 + 8 + 8)
        
        private TResult result;
        private object error;//ExceptionHolder 或 OperationCanceledException
        private short version;
        private bool hasUnhandledError;
        private int completedCount;//0: completed == false
        private Action<object> continuation;
        private object continuationState;

        public short Version => this.version;

        public void Reset()
        {
            this.ReportUnhandledError();

            unchecked
            {
                this.version += 1;//version 自增
            }
            this.completedCount = 0;
            this.result = default;
            this.error = null;
            this.hasUnhandledError = false;
            this.continuation = null;
            this.continuationState = null;
        }

        private void ReportUnhandledError()
        {
            if (this.hasUnhandledError)
            {
                try
                {
                    if (this.error is OperationCanceledException oc)
                    {
                        STaskScheduler.PublishUnobservedTaskException(oc);
                    }
                    else if (this.error is ExceptionHolder e)
                    {
                        STaskScheduler.PublishUnobservedTaskException(e.GetException().SourceException);
                    }
                }
                catch { }
            }
        }

        internal void MarkHandled()
        {
            this.hasUnhandledError = false;
        }

        /// <summary>
        /// 成功执行
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TrySetResult(TResult result)
        {
            //if 'completedCount' is 0 before invoke Increment Method，只有未被使用过的才能设置，即只能设置一次结果（不论是 successful/exception/canceled）
            if (Interlocked.Increment(ref this.completedCount) == 1)
            {
                this.result = result;

                //if 'continuation' is not null (has set in OnCompleted correctly), invoke continuation,
                //if 'continuation' is null, set it to 's_sentinel' in order to avoid invoke it again
                if (this.continuation != null || Interlocked.CompareExchange(ref this.continuation, STaskCompletionSourceCoreShared.s_sentinel, null) != null)
                {
                    this.continuation(this.continuationState);               
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行中遇到异常
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool TrySetException(Exception error)
        {
            if (Interlocked.Increment(ref this.completedCount) == 1)
            {
                this.hasUnhandledError = true;
                if (this.error is OperationCanceledException)
                {
                    this.error = error;
                }
                else
                {
                    this.error = new ExceptionHolder(ExceptionDispatchInfo.Capture(error));
                }

                if (this.continuation != null || Interlocked.CompareExchange(ref this.continuation, STaskCompletionSourceCoreShared.s_sentinel, null) != null)
                {
                    this.continuation(this.continuationState);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行时被取消
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this.completedCount) == 1)
            {
                this.hasUnhandledError = true;
                this.error = new OperationCanceledException(cancellationToken);

                if (this.continuation != null || Interlocked.CompareExchange(ref this.continuation, STaskCompletionSourceCoreShared.s_sentinel, null) != null)
                {
                    this.continuation(this.continuationState);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取执行状态
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public STaskStatus GetStatus(short token)
        {
            this.ValidateToken(token);
            return (this.continuation == null || this.completedCount == 0) ? STaskStatus.Pending
                : (this.error == null) ? STaskStatus.Succeeded
                : (this.error is OperationCanceledException) ? STaskStatus.Canceled
                : STaskStatus.Faulted;
        }

        /// <summary>
        /// 不检查 token 是否合法，获取执行状态
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public STaskStatus UnsafeGetStatus()
        {
            return (this.continuation == null || this.completedCount == 0) ? STaskStatus.Pending
                : (this.error == null) ? STaskStatus.Succeeded
                : (this.error is OperationCanceledException) ? STaskStatus.Canceled
                : STaskStatus.Faulted;
        }

        /// <summary>
        /// 获取执行结果
        /// </summary>
        /// <param name="token"> 执行<see cref="STask"/>构造方法时传递的值 </param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public TResult GetResult(short token)
        {
            this.ValidateToken(token);
            if (this.completedCount == 0)
            {
                throw new InvalidOperationException("Not yet completed, STask only allow to ues await.");
            }

            if (this.error != null)
            {
                this.hasUnhandledError = false;
                if (this.error is OperationCanceledException oce)
                {
                    throw oce;
                }
                else if (this.error is ExceptionHolder eh)
                {
                    eh.GetException().Throw();
                }

                throw new InvalidOperationException("Critical: invalid exception type was held.");
            }

            return this.result;
        }

        /// <summary>
        /// 调度 continuation
        /// </summary>
        /// <param name="continuation">执行完毕后，被调用的回调</param>
        /// <param name="state">回调的参数，在 STask 中，它是真正的 continuation，具体可 查看 STask.Awaiter.OnCompleted()</param>
        /// <param name="token"> 执行<see cref="STask"/>构造方法时传递的值 </param>
        /// <exception cref="ArgumentNullException">continuation is null</exception>
        public void OnCompleted(Action<object> continuation, object state, short token /*, ValueTaskSourceOnCompletedFlags flags */)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }
            this.ValidateToken(token);
            
            //不使用 ValueTaskSourceOnCompletedFlags 形参，不处理执行上下文或同步上下文
            
            //异步方法执行情况：
            //PatternA: GetStatus = Pending => OnCompleted => TrySet*** => GetResult
            //PatternB: TrySet*** => GetStatus = !Pending => GetResult  此时 this.continuation 为 s_sentinel
            //PatternC: GetStatus = Pending => OnCompleted/TrySet*** (race condition) => GetResult
            //C.1 win OnCompleted => TrySet*** invoke saved continuation
            //C.2 win TrySet*** => should invoke continuation here
            
            //重点在第三种情况
            //若先执行的是 OnCompleted，那么 continuation 会在这里保存下来，供 TrySet*** 方法执行
            //若先执行的是 TrySet***，那么在这里执行 continuation

            object oldContinuation = this.continuation;
            if (oldContinuation == null)//还没设置 continuation
            {
                this.continuationState = state;
                oldContinuation = Interlocked.CompareExchange(ref this.continuation, continuation, null);
                //此时 oldContinuation 仍然为 null，this.continuation 为形参 continuation
                //之后会在 TrySet 中调用 this.continuation
            }

            if (oldContinuation != null)
            {
                //先执行的 TrySet，此时 oldContinuation 为 s_sentinel，我们需要在这里调用回调
                //若 oldContinuation != s_sentinel，表示多次 await 了同一个 STask，这是不允许的
                if (!ReferenceEquals(oldContinuation, STaskCompletionSourceCoreShared.s_sentinel))
                {
                    throw new InvalidOperationException("Already continuation registered, can not await twice or get Status after await.");
                }

                continuation(state);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateToken(short token)
        {
            if (token != this.version)
            {
                throw new InvalidOperationException("Token version is not matched, can not await twice or get Status after await.");
            }
        }
    }

    //与通用逻辑分开，避免不必要的拷贝
    internal static class STaskCompletionSourceCoreShared
    {
        internal static readonly Action<object> s_sentinel = CompletionSentinel; 

        private static void CompletionSentinel(object _)
        {
            throw new InvalidOperationException("The sentinel delegate should never be invoked.");
        }
    }

    internal class ExceptionHolder
    {
        private ExceptionDispatchInfo exception;
        private bool calledGet = false;

        public ExceptionHolder(ExceptionDispatchInfo exception)
        {
            this.exception = exception;
        }

        public ExceptionDispatchInfo GetException()
        {
            if (!this.calledGet)
            {
                this.calledGet = true;
                GC.SuppressFinalize(this);//通知GC不再调用析构函数，防止在Finalize线程调用析构函数之后，GC再重复调用
            }
            return this.exception;
        }

        ~ExceptionHolder()
        {
            if (!this.calledGet)
            {
                STaskScheduler.PublishUnobservedTaskException(this.exception.SourceException);
            }
        }
    }
```

在注释中，标出了 `STaskCompletionSourceCore` 成员变量所占用的字节大小，它们在结构中的作用分别是：

- `result` ：异步方法的返回值，即调用使用 `await` 关键字后的返回值。
- `error` ：遇到异常后或等待被取消后，会调用 `TrySetException` / `TrySetCanceled` 并将异常对象赋值给它，因此它可能普通异常对象包装后的 `ExceptionHolder` 或取消异常 `OperationCanceledException` 。我们需要将取消异常与其他异常进行区分，因为我们要识别 `STask` 的具体运行状态。
- `version` ：用于标识当前 `STaskCompletionSourceCore` 对象的 'token'。
- `hasUnhandledError` ：遇到异常后，我们会将它设置为 `true` ，在 `core` 结束当前的寿命周期前，我们会通过它来判断是否有异常，若有则需要将异常派发出来。
- `completedCount` ：我们通过它来判断 `core` 的工作有没有完成。当上层调用 `TrySet***` 的方法后，就表示 `core` 的工作完成了。
- `Action<object>` ：等待完成后的回调委托。
- `continuationState` ：回调委托的参数。

其中最重要的是 `version` 变量。`STask` 的每次等待操作，底层都是由 `core` 来完成的。前面我们也提到，为了优化性能，我们使用了对象池，而 `core` 就是我们要“池化”的对象。但问题来了：由于 `core` 是可被复用的，当用户没有直接 `await` 异步方法，而是将异步方法返回的 `STask` 给保存下来，并在后续多次等待：

```c#
STask task = MethodAsync();
await task;
await task;//不允许，抛出异常
```

上面代码的前两行没什么问题，但第三行对同一 `STask` 的多次等待，会导致意想不到的后果。在 `core` 结构的逻辑中，当它被重新放入对象池，需要将成员变量都重置一遍，以便下次使用，这里的成员变量就包括了返回值。在 STask 异步库中，我们会在状态机调用 `awaiter.GetResult`  时，在底层调用 `core.GetResult` ，然后重置 `core` 对象并放入对象池：

![STaskCompletionSource0](D:\SaveData\OneDrive\markdown\STask\images\STaskCompletionSource0.png)

从流程图中可以看到，第一次 `await` 后，再次 `await` ，并不能保证能得到和第一次一致的结果（若 STask 有返回值），因为 `core` 已经被重置并回收了。这也是使用使用 `STask` 的限制所在，**不要多次 `await` 同一个 `STask`** 。实际上，.NET 提供的 [ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1?view=netcore-3.1) 也有这样的限制，它们内部都采用 `version` 来判断当前的 `core` 是否在有效期内（同样的，也可以理解为 `STask` 是否有效，在它俩的生命周期中，`version` 相等）。为了限制用户多次等待，我们在 `core` 内部的一些地方增加了判断当前 `version` 的步骤（`ValidateToken` 方法)。当需要对 `core` 进行操作时，需要从外部传入 token——也就是申请 `core` 时它的 `version` 的值来进行验证。

下面我们来讨论 `core` 拥有的成员方法：

- `Reset` ：上面提到的，在 `core` 被回收进池前需要重置成员变量，“重置”这一步骤就是调用 `Reset` 方法。该方法会先调用 `ReportUnhandledError` 处理异常，然后将 `version` 的值自增 1，把其余的变量设置为默认值。为了防止自增 `version` 发生溢出，我们用 `unchecked` 语句将它框起来。
- `ReportUnhandledError` ：将存在的异常派发出去，里面用到的 `STaskScheduler` 会在之后讨论。
- `MarkHandled` ：提供标记异常已解决的接口。
- `TrySetResult` ：异步方法成功执行，将执行结果赋值给 `result` ，然后视情况调用 `continuation` ，即 `await` 语句之后的代码。
- `TrySetException` ：异步方法执行中遇到异常，将异常对象赋值给 `error` ，然后视情况调用 `continuation` 。
- `TrySetCanceled` ：异步方法执行中被取消，申请一个 `OperationCanceledException` 对象，将其赋值给 `error` ，最后视情况调用 `continuation` 。
- `GetStatus` ：获取当前执行状态，调用 `awaiter.IsCompleted` 时会执行到该方法（一般情况下）。
- `UnsafeGetStatus` ：也是获取当前运行状态，不检查 token 是否合法。该方法一般用于调试，平时用不到。
- `GetResult` ：获取当前的执行结果。返回结果前，会进行一系列合法性判断： `version` 变量、方法是否执行完毕、是否有未处理异常。调用 `awaiter.GetResult` 时会执行到该方法（一般情况下）。
- `ValidateToken` ：判断 token 合法性。
- `OnCompleted` ：处理 `await` 语句之后的回调。该方法与 [ManualResetValueTaskSource.OnCompleted](https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Sources/ManualResetValueTaskSourceCore.cs,120) 方法类似，调用 `awaiter.OnCompleted` 时会执行到该方法，但去掉了处理上下文的逻辑——因为我们不需要，以此优化性能。该方法虽然代码量不大，却是 `STaskCompletionSourceCore` 中最难理解的，因为它不能拿到回调方法后就直接调用，调用回调方法的时机是有点小讲究的，这里与上述三个 `TrySet***` 方法有密切的关联，下面我们把它们放在一起讨论。

假如有以下代码：

```c#
public async STask<float> MethodAsync()
{
    float thisTime = Time.realtimeSinceStartup;
    await STask.Delay(5000);//等待5秒
    return Time.realtimeSinceStartup - thisTime;
}

public void Start()
{
    float awaitTime = await MethodAsync();
    Debug.Log($"consume time: {awaitTime}s");
}
```

观察上面的代码片段，我们有两个 STask:

1. MethodAsync 方法的返回值 `STask<float>` 。
2. STask.Delay 方法的返回值 STask 。

其中只有第一个 STask<float> 是作为**异步方法**的一部分存在的，因此它会走与之关联的异步状态机机制，也就是我们的自定义状态机。

而第二个 STask 并不是异步方法的一部分，因为 STask.Delay 方法并没有用 async 关键字标记，其内部也就没有 await 语句，因此它就不会走自定义状态机。（关于 STask.Delay 实现，我们会在后面讨论，大伙在这里只需要其内部依靠的是 Unity 提供的 PlayerLoop System 实现的，不涉及多线程）。

下面我们来看看这段代码的异步流程：



![STaskCompletionSource1](D:\SaveData\OneDrive\markdown\STask\images\STaskCompletionSource1.png)

上图演示了异步方法执行时的**大致**流程（实线箭头表示消息发出，虚线箭头表示消息返回）：

1. 异步方法 `MethodAsync` 执行到 await 语句，状态机会自动调用被等待对象的 `GetAwaiter` 方法获取 `Awaiter` ，其中被等待对象是 `STask.Delay` 方法返回的 `STask` 。在状态机获取到 `Awaiter` 时，已经走过上图的步骤①到步骤④。
2. 然后，状态机会通过 `Awaiter.IsCompleted` 字段判断被等待对象（即 `STask` ）是否完成了工作。若完成，则调用 `Awaiter.GetResult` 获取结果并将其返回给 await 语句的左值（上面示例代码的 `STask` 是无返回值的，但也会执行“获取结果”的步骤，只不过不会返回任何结果）。若没有完成，则调用 `AwaitOnCompleted` ，把 await 语句后面的代码打包成回调方法，告诉 `Awaiter` ，意思是当 `Awaiter` 完成工作后，调用该回调，达到继续执行后面代码的效果。小伙伴可能会有疑惑，状态机除了有 `AwaitOnCompleted` 方法外，还有 `AwaitUnsafeOnCompleted` ，它们有什么区别呢？当 `Awaiter` 实现的是 `INotifyCompletion` 接口时，状态机将调用前者，实现的是 `ICriticalNotifyCompletion` 时，调用的是后者，具体可参考：[What is ICriticalNotifyCompletion for?](https://stackoverflow.com/questions/65529509/what-is-icriticalnotifycompletion-for) 。到此，我们执行了上图的步骤⑤到步骤⑥。
3. STask 的底层核心机制都由 `STaskCompletionSourceCore` 来承担，因此 `Awaiter` 会将回调传递给 `core` ，即执行步骤⑦、⑧。

上面的流程都是按照先后顺序执行的。在步骤②，当 `STask.Delay` 创建 `STask` 对象时，`STas` 内部也创建了 `DelayPromise` ，它是实现延迟功能的对象，实现了 ISTaskSource 接口。当设置的延迟时间结束， `DelayPromise` 会调用 `TrySetResult` / `TrySetException` 通知 `STaskCompletionSourceCore` 任务结束。在这里，成功、失败或者取消都视作任务结束，因此我们在后面将任务结束后调用的方法统称为 `TrySet***` 。

梳理完流程，现在我们还剩下一个最关键的问题，什么时候去调用回调方法？

当然是在异步任务结束时调用啦！也就是说，当 `TrySet***` 被调用时，我们就该调用回调方法了。

但真是如此简单吗？动动我们的小脑瓜想想，难道 `TrySet***` 方法被调用的时机，一定在 `core.OnCompleted` 之后吗？

事实并非如此，一切的一切都归根到两个字——“异步”。我们无法提前知道任务执行需要的时间（一般情况下），也无法预测任务是否会被取消或者遇到异常，因此**我们无法判断 `TrySet***` 被调用的准确时机**。

理想情况下，我们希望先执行 `core.OnCompleted` 方法，把回调方法传递给 `core` ，然后等待任务结束调用 `core.TrySet***` ，并在 `TrySet***` 中调用回调。但现实有以下几种情况：

- `core.GetStatus` = Pending → `core.Complted` → `core.TrySet***` → `core.GetResult` ：最容易理解的情况，即上面提到的理想情况。
- `TrySet***` → `GetStatus` != Pending → `GetResult` ：在异步状态机等待前，任务就已经结束了，直接获取结果。
- `GetStatus` = Pending → `OnComplted` / `TrySet***` → `GetResult` ：第一种情况的超集，在等待任务时，`OnComplted` 和 `TrySet***` 处于“竞争”关系中——它们任何一个都可能会比另一个先被调用。其实，第一种情况只是为了方便小伙伴们理解，从这里拆分出去的。

到这里，我们的问题变成了怎样在 `OnComplted` 和 `TrySet***` 的竞争关系中，处理异步方法的回调：

1. 先调用了 `OnComplted` ，异步任务还未结束，那么我们将传进来的回调方法保存下来，供 `TrySet***` 在后面执行。
2. 先调用了 `TrySet***` ，异步任务已结束，那么直接执行回调方法。

`STaskCompletionSourceCore` 最核心的逻辑就是这么多，具体的代码已经在上面给出了，并且必要的地方已经做好了注释，这里就不再逐行分析了哟~

### ExceptionHolder

在 `STaskCompletionSourceCore` 中，我们使用了类 `ExceptionHolder` 来处理异常，我们这样做的目的有：

- 将 `OperationCanceledException` （后文将其简称为“OCE”）与其他异常区分开。在C#中，想优雅地取消某个操作，我们一般通过触发“操作取消”异常，即 “OCE” 来完成。并且在处理时，“OCE” 与其它异常的处理方式通常也有区别，因此我们最好将它从 `Exception` 大类中抽离出来，而剩下的异常类型，我们就用 `ExceptionHolder` 包装起来，这样就只有两种异常了（对于 `STaskCompletionSourceCore` 来说）。
- 确保不会遗漏未处理的异常。我们会在 `ExceptionHolder` 的析构方法中检测所包装的异常对象是否被处理过，若没有，我们需要将它抛出。当外部通过 `ExceptionHolder.GetException` 方法获取了异常对象，我们就认为异常已被处理，通过 `GC.SuppressFinalize` 接口通知垃圾回收机制不再调用析构方法，防止抛出不该抛出的异常。

### STaskCompletionSourceCoreShared

由于 STask 是不允许被多次等待的——这样可能会导致用户得到不正确的结果，因此我们需要在用户多次使用 `await` 语句等待同一 STask 时，抛出异常。

在哪里检测 STask 是否被多次等待了呢？当用户等待 STask 时，代码最终会执行到 `core.OnComplted` 和 `TrySet***` 方法，因此我们需要在里面添加检测代码，具体逻辑可以查看上方贴出来的源码。下面是技术要点：

- 执行到 `core.OnCompleted` ，检查是否保存过回调方法：
  1. 若没有保存，即第一次调用 `OnCompleted` ，则将回调方法保存下来，供 `TrySet***` 调用。
  2. 若已经保存过，则检查回调是否与 `STaskCompletionSourceCoreShared.s_sentinel` 相等，若不相等，表明出现了多次等待的情况，即多次调用了 `OnCompleted` ，需要抛出异常；若相等，表明先执行了 `TrySet***` ，因此直接执行回调方法。
- 执行到 `core.TrySet***` ，检查是否保存过回调方法：
  1. 若保存过，表明 `OnCompleted` 已经执行，这里直接执行回调方法。
  2. 若没有保存过，表明先执行了 `TrySet***` ，我们需要将回调设置成 `s_sentinel` ，避免多次等待 STask。
  3. 把回调设置成 `s_sentinel` 这一步的处理比较巧妙，利用了C#条件或语句的特性，大伙可以自行感受下。
- 由于 `STaskCompletionSourceCoreShared.s_sentinel` 只被用于跟其他方法进行比较，其本身不会有变化，所以我们可以把它定义为静态类型，优化性能。



## STaskScheduler

在 `STaskCompletionSourceCore` 和 `ExceptionHolder` 中，我们用到了 `STaskScheduler` 来处理异常——把异常对象发送出去，至于发出去之后具体怎么处理，则交给 `STaskScheduler` 负责。

其实，.NET Framework 就提供了 `TaskScheduler` 给用户使用，它的作用是接受外部传来的任务，将这些任务派发到相关的线程上进行处理，并且它能够保证这些任务最终能够被执行。

我们的 `STaskScheduler` 也是类似的，但我们只需要它处理异常，因此对它做了一些“简化”。实例代码如下：

```c#
	/// <summary>
    /// STask 没有类似 TaskScheduler 的 scheduler，该类只处理未处理的异常
    /// </summary>
    public static class STaskScheduler
    {
        public static event Action<Exception> UnobservedTaskException;

        /// <summary>
        /// 是否调用 UnobservedTaskException 来处理 OperationCanceledException；默认为 false
        /// </summary>
        public static bool PropagateOperationCanceledException = false;

        /// <summary>
        /// 捕获到未处理异常且没绑定 UnobservedTaskException 时，写到 UnityLog 的 LogType；默认为Exception
        /// </summary>
        public static UnityEngine.LogType UnobservedExceptionWriteLogType = UnityEngine.LogType.Exception;

        /// <summary>
        /// 是否将异常发回 Unity 主线程；默认为 true
        /// </summary>
        public static bool DispatchUnityMainThread = true;

        private static void InvokeUnobservedTaskException(object state)
        {
            UnobservedTaskException((Exception)state);
        }
        
        /// <summary>
        /// 委托缓存
        /// </summary>
        private static readonly SendOrPostCallback handleExceptionInvoke = InvokeUnobservedTaskException;

        internal static void PublishUnobservedTaskException(Exception ex)
        {
            if (ex != null)
            {
                if (!PropagateOperationCanceledException && ex is OperationCanceledException)
                {
                    return;
                }

                if (UnobservedTaskException != null)
                {
                    //使用外部提供的处理方法，需要考虑主线程与子线程的问题
                    if (!DispatchUnityMainThread || Thread.CurrentThread.ManagedThreadId == MainThreadId)
                    {
                        UnobservedTaskException.Invoke(ex);
                    }
                    else
                    {
                        UnitySynchronizationContext.Post(handleExceptionInvoke, ex);
                    }
                }
                else
                {
                    string msg = null;
                    if (UnobservedExceptionWriteLogType != UnityEngine.LogType.Exception)
                    {
                        msg = "UnobservedTaskException: " + ex.ToString();
                    }
                    switch (UnobservedExceptionWriteLogType)
                    {
                        case UnityEngine.LogType.Error:
                            UnityEngine.Debug.LogError(msg);
                            break;
                        case UnityEngine.LogType.Assert:
                            UnityEngine.Debug.LogAssertion(msg);
                            break;
                        case UnityEngine.LogType.Warning:
                            UnityEngine.Debug.LogWarning(msg);
                            break;
                        case UnityEngine.LogType.Log:
                            UnityEngine.Debug.Log(msg);
                            break;
                        case UnityEngine.LogType.Exception:
                            UnityEngine.Debug.LogException(ex);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        
        public static int MainThreadId { get; private set; } 
        public static SynchronizationContext UnitySynchronizationContext { get; private set; }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            UnitySynchronizationContext = SynchronizationContext.Current;
        }
    }
```

重要的部分我已经做了些注释，其中最重要的部分就是主线程与子线程的问题。

默认情况下，`STaskScheduler` 是利用 `UnityEngine.Debug.LogException` 接口将异常对象抛出给用户，但它也提供了自定义处理接口 `UnobservedTaskException` 。

当使用自定义处理接口时，我们就需要考虑“当前是否在主线程”这个问题。比如，用户在其他线程中调用了 `STaskScheduler` ，但自定义接口绑定的是需要运行在 Unity 主线程里的方法，这就会导致出错。

将委托发送到主线程这一步骤，使用了 `SynchronizationContext.Post` 接口。我们在 Unity 初始化时将主线程同步上下文的 ID 和实例记录下来（此时必定在主线程中），然后在需要的时候调用它就好，就是这么简单嗷。

在 `UniTask/STask` 库里，跟主线程相关的设置都在 `PlayerLoopHelper` 类中，这里为了方便展示，我暂时将它分离了出来。

`STaskScheduler` 带给我们的启示远不止这些，在我们遇到需要将一些代码派发到主线程执行的情况时，也可以套用这一套组合拳。举个笔者的亲身例子，当使用 `ISerializationCallbackReceiver` 接口时，我们需要在接口方法 `OnBeforeSerialize` 或 `OnAfterDeserialize` 中直接调用 Unity 接口是不可以的，因为它们都在非主线程中执行，因此只能采用将代码派发到主线程执行的方法来实现。



## STask/Awaiter

介绍了最重要的 `STaskCompletionCore` ，接下来我们讨论处于总览图另一边的 `STask` 以及和它配对的 `Awaiter` 。

上代码！

```c#
    public readonly struct STask<T>
    {
        private readonly ISTaskSource<T> source;
        private readonly T result;
        private readonly short token;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public STask(T result)
        {
            this.source = default;
            this.token = default;
            this.result = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public STask(ISTaskSource<T> source, short token)
        {
            this.source = source;
            this.token = token;
            this.result = default;
        }

        public STaskStatus Status
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (this.source == null) ? STaskStatus.Succeeded : this.source.GetStatus(this.token);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Awaiter GetAwaiter()
        {
            return new Awaiter(this);
        }

        public override string ToString()
        {
            return (this.source == null) ? this.result?.ToString()
                : "(" + this.source.UnsafeGetStatus() + ")";
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly STask<T> task;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Awaiter(in STask<T> task)
            {
                this.task = task;
            }

            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return this.task.Status.IsCompleted();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetResult()
            {
                ISTaskSource<T> source = this.task.source;
                if (source == null)
                {
                    return this.task.result;
                }
                else
                {
                    return source.GetResult(this.task.token);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnCompleted(Action continuation)
            {
                ISTaskSource<T> source = this.task.source;
                if (source == null)
                {
                    continuation();
                }
                else
                {
                    source.OnCompleted(AwaiterActions.InvokeContinuationDelegate, continuation, this.task.token);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void UnsafeOnCompleted(Action continuation)
            {
                ISTaskSource<T> source = this.task.source;
                if (source == null)
                {
                    continuation();
                }
                else
                {
                    source.OnCompleted(AwaiterActions.InvokeContinuationDelegate, continuation, this.task.token);
                }
            }
        }
    }
```

我们先来讨论 `STask<T>` 的成员：

- `source` ：接口 `ISTaskSource<T>` 实例，它是实现异步机制的对象。在返回值为 `STask/STask<T>` 的异步方法的异步机制中，这个对象是 `AsyncSTask` ，这里用接口来声明的目的是为了后期的扩展性。
- `result` ：异步方法执行完之后的结果。
- `token` ：时效令牌，在这里用来给 `STaskCompletionSourceCore` 验证，即与它的 `version` 相比。在创建 `STask` 时，我们也会创建 `core` ，并将 `core.Version` 告诉给 `STask` ，到 `STask` 这里就是 `token` 。给同一个变量起两个名字，完全是使用场景不同的缘故。
- `Status` ：当前 `STask` 当前的执行状态。

- `STask(ISTaskSource<T> source, short token)` ：构造方法，一般来说我们会调用它来创建 STask、Awaiter。
- `STask(T result)` ：构造方法，但参数是异步方法的结果。有时在创建 STask 之前，我们会判断任务是否被取消（或者出现异常），这时我们可以直接将默认结果告知给 STask；或者我们需要构建已完成任务的 STask 时，也可以使用该构造方法。
- `GetAwaiter()` ：没有它，我们就不能使用 await 语句等待 STask ，当然其返回的 Awaiter 需要有 `IsCompleted` 、 `GetResult()` 、 `OnCompleted()/UnsafeOnCompleted()` 。



接下来我们讨论 `Awaiter` 的成员：

- `task` ：创建该 Awaiter 的 STask，我们要通过它来获取异步逻辑需要的信息。
- `IsCompleted` ：返回当前异步任务的执行状态，任务刚开始时，状态机会访问它来判断任务是否完成，若已经完成则直接返回。
- `Awaiter(STask<T> task)` ：平平无奇的构造方法。
- `GetResult()` ：异步任务结束后，状态机会调用它来获取结果，并返回。
- `OnCompleted()/UnsafeOnCompleted()` ：当异步任务需要等待（即不是以来就已完成），状态机会调用它来注册回调。

Awaiter 的方法 `GetResult()` 、 `OnCompleted()/UnsafeOnCompleted()` 中我们会判断 `task.source` 是否为空：若为空，则表明是通过 `STask(T result)` 创建的，那获取结果的就直接返回，注册回调的就直接调用回调（因为异步任务已经结束了）；若不为空，则表明是通过 `STask(ISTaskSource<T> source, short token)` 创建的，我们就需要用 `task.source` 来处理相关逻辑。



## AsyncSTask

现在我们来讨论 `AsyncSTask<TStateMachine, T>` ，我们这里举例的是有返回值版本，后面我们将它简称为 `AsyncSTask` 。

`AsyncSTask` 实现了一共三个接口，其中 `ITaskPoolNode<AsyncSTask>` 接口表示它将自己“池化”，变成可以放进对象池里的结构——没错，我们需要缓存 `AsyncSTask` 。第二个接口 `IStateMachineRunnerPromise<T>` ，是在状态机 `AsyncSTaskMethodBuilder<T>` 中被引用的，简而言之，状态机通过该接口这种“抽象对象”与 `AsyncSTask` 建立了连接。有异曲同工之妙的第三个接口 `ISTaskSource<T>` ，在 `STask<T>` 中被引用，具体用法可以参照上一小节。

其中最需要加深理解的是 `ISTaskSource<T>` 接口，其次是 `IStateMachineRunnerPromise<T>` 。

我们先讲讲简单后者 `IStateMachineRunnerPromise<T>` ：C#的接口，是非常强大的解耦工具，使用者只需要关心接口帮我们抽象出来的方法，可以完全不关心接口的实现，并且百分百相信接口的实现者不会出问题。既然我只关心接口的方法，那么具体是谁来实现跟我也毫无关系（这种说法比较绝对，因为有时候使用者也是知道接口的具体实现者的，但让它不知道是我们使用接口的目的）。`IStateMachineRunnerPromise<T>` 被状态机使用，状态机用它来实现具体的逻辑，当我们想更换 `AsyncSTask` 这个打工仔的时候，只需要自己在实现一次 `IStateMachineRunnerPromise<T>` 接口即可。

接下来是接口 `ISTaskSource<T>` ：之所以说它更重要，是因为在后续扩展 `STask` 库的工作中，我们免不了跟它打交道。上面提到我们可以自己实现 `IStateMachineRunnerPromise<T>` ，替换掉现在的 `AsyncSTask` ，但我们几乎不会这样做——因为 `AsyncSTask` 已经做得非常好了。那么 `ISTaskSource<T>` 是如何帮助我们扩展 `STask` 库的呢？我们继续用下面的代码来举例：

```c#
public async STask<float> MethodAsync()
{
    float thisTime = Time.realtimeSinceStartup;
    await STask.Delay(5000);//等待5秒
    return Time.realtimeSinceStartup - thisTime;
}

public void Start()
{
    float awaitTime = await MethodAsync();
    Debug.Log($"consume time: {awaitTime}s");
}
```

这段代码一共有两处使用了 await 的，按照使用顺序来，分别是等待 `MethodAsync()` 和 `STask.Delay()` 。

- `MethodAsync()` 是异步方法，它会返回一个 STask 对象，并且该 STask 对象的 ISTaskSource 接口实例为 AsyncSTask。AsyncSTask 就会帮助 STask 来处理异步相关的逻辑。
- `STask.Delay` 不是异步方法，但它仍然会返回一个 STask 对象，因此我们可以用 await 去等待它。它返回的 STask 对象的 ISTaskSource 接口实例是我们自己实现的 `DelayPromise` ，它会帮我们计时，当到了设定的时间后，它会通知 `STaskCompletionSourceCore` （`DelayPromise` 内部仍然依赖 `STaskCompletionSourceCore` ，通知即调用 `core.TrySetResult` ），这时等待任务结束。

有机会的话，我们会在后面单独讨论 STask 库的扩展。

下面是 `AsyncSTask` 源码：

```c#
internal sealed class AsyncSTask<TStateMachine, T> : IStateMachineRunnerPromise<T>, ISTaskSource<T>, ITaskPoolNode<AsyncSTask<TStateMachine, T>>
        where TStateMachine : IAsyncStateMachine
    {
        private TStateMachine stateMachine;
        private STaskCompletionSourceCore<T> core;

        private AsyncSTask()
        {
            this.MoveNext = this.Run;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Run()
        {
            this.stateMachine.MoveNext();
        }


        #region Pool
        private static TaskPool<AsyncSTask<TStateMachine, T>> pool;

        public static void SetStateMachine(ref TStateMachine stateMachine, ref IStateMachineRunnerPromise<T> runnerPromiseFieldRef)
        {
            if (!pool.TryPop(out var result))
            {
                result = new AsyncSTask<TStateMachine, T>();
            }

            runnerPromiseFieldRef = result; // set runner before copied.
            result.stateMachine = stateMachine; // copy struct StateMachine(in release build).
        }

        private AsyncSTask<TStateMachine, T> nextNode;
        public ref AsyncSTask<TStateMachine, T> NextNode => ref this.nextNode;

        static AsyncSTask()//静态构造函数，只执行一次（实例化前或引用其他静态成员前调用一次）
        {
            TaskPool.RegisterSizeGetter(typeof(AsyncSTask<TStateMachine, T>), () => pool.Size);
        }
        #endregion

        private void Return()
        {
            this.core.Reset();
            this.stateMachine = default;
            pool.TryPush(this);
        }

        private bool TryReturn()
        {
            this.core.Reset();
            this.stateMachine = default;
            return pool.TryPush(this);
        }

        #region IStateMachineRunnerPromise<T>
        public Action MoveNext { get; }

        public STask<T> Task
        {
            get
            {
                return new STask<T>(this, this.core.Version);
            }
        }

        public void SetException(Exception exception)
        {
            this.core.TrySetException(exception);
        }

        public void SetResult(T result)
        {
            this.core.TrySetResult(result);
        }
        #endregion

        #region ISTaskSource<T>
        public T GetResult(short token)
        {
            try
            {
                return this.core.GetResult(token);
            }
            finally
            {
                this.TryReturn();
            }
        }

        void ISTaskSource.GetResult(short token)
        {
            this.GetResult(token);
        }

        public STaskStatus GetStatus(short token)
        {
            return this.core.GetStatus(token);
        }

        public STaskStatus UnsafeGetStatus()
        {
            return this.core.UnsafeGetStatus();
        }

        public void OnCompleted(Action<object> continuation, object state, short token)
        {
            this.core.OnCompleted(continuation, state, token);
        }
        #endregion
    }
```

小编已经将代码按接口进行分类了，下面我们就按照这种分类来大致讨论下 `AsyncSTask` 的成员：

- `IStateMachineRunnerPromise<T>` ：实现它，你就能成为状态机大名鼎鼎的打工仔！
  - `Task` ：状态机通过它获取可等待对象（在这里为 STask 类型）。
  - `SetException(Exception)` ：执行中遇到异常，状态机调用它来处理。
  - `SetResult(T)` ：执行完毕，状态机调用它设置结果/返回值。
  - `Action MoveNext` ：该接口下最重要的成员，`MoveNext` 委托绑定状态机的 `MonveNext()` 方法（具体可看构造方法 `AsyncSTask()` 和 `Run()` 方法），该委托会被状态机注册给 awaiter，当任务执行完毕，该委托会被调用。
- `ISTaskSource<T>` ：跟 STask 结合的密码，实现了它，STask 才会答应和我们一起玩。
  - `GetStatus(short token)` ：获取当前任务的执行状态，一般用于在等待开始前判断任务是否已经执行完毕，避免再次等待造成性能浪费。
  - `OnCompleted(Action<object> continuation, object state, short token)` ：用于注册回调方法。
  - `T GetResult(short token)` ：任务结束后，获取任务结果。
- `ITaskPoolNode<T>` ：实现 `AsyncSTask` 的对象池。
  - `TaskPool<T> pool` ：对象池本池。
  - `SetStateMachine(ref TStateMachine stateMachine, ref IStateMachineRunnerPromise<T> runnerPromiseFieldRef)` ：该方法被状态机调用，状态机通过它来获取 `AsyncSTask` 对象，同时把自身（状态机对象）传递给 `AsyncSTask` 。
  - `AsyncSTask<T> NextNode` ：指向对象池中的下一个 `AsyncSTask` 对象。对象池没有单独的容器来保存所有池中的对象，而是采用链表的方式来组织它们。

- 除此之外，`AsyncSTask` 还拥有自身的成员：
  - `Run()` ：该方法被绑定到 `MoveNext` 委托，并在内部调用状态机的 `MoveNext()` 方法，推动状态机运行。小伙伴可能会疑惑，为什么不直接将 `MoveNext` 委托绑定 `MoveNext()` 方法，而要中间再加一层 `Run()` ，笔者推测这是为了方便在调试时查看调用堆栈所做的操作。
  - `Return()` ：在 `GetResult` 方法中调用，当任务完成后，我们最终需要重置用过的对象，并将自身放入对象池。
  - `TryRetuen()` ：`Return()` 的有返回值版，实际上我们一般用它，而不是 `Return()` 。



## AsyncSTaskMethodBuilder

终于轮到它了——`AsyncSTaskMethodBuilder` 作为异步状态机的一部分，我们可以把它看作异步状态机对外提供的可自定义接口。和 `AsyncSTask` 一样，它也有带返回值类型的泛型变体（我们在后面讨论的也是这个），为了方便，我们就直接称呼它为 `AsyncSTaskMethodBuilder` 、`builder` 或 “状态机”。

其实我们在第一章里已经讨论过了 “Builder Type”，知道了自定义状态机的特征和需要遵循的规则。STask 中的 `builder` 几乎没有变化，因此我们的研究重点落在了状态机成员方法的实现上。

```c#
	[StructLayout(LayoutKind.Auto)]
    public struct AsyncSTaskMethodBuilder<T>
    {
        private IStateMachineRunnerPromise<T> runnerPromise;
        private Exception ex;
        private T result;

        // 1. Static Create method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncSTaskMethodBuilder<T> Create()
        {
            return default;
        }

        // 2. TaskLike Task property
        public STask<T> Task
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.runnerPromise != null)
                {
                    return this.runnerPromise.Task;
                }
                else if (this.ex != null)
                {
                    return STask.FromException<T>(this.ex);
                }
                else
                {
                    return STask.FromResult(this.result);
                }
            }
        }

        // 3. SetException
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception exception)
        {
            if (this.runnerPromise == null)
            {
                this.ex = exception;
            }
            else
            {
                this.runnerPromise.SetException(exception);
            }
        }

        // 4. SetResult
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(T result)
        {
            if(this.runnerPromise == null)
            {
                this.result = result;
            }
            else
            {
                this.runnerPromise.SetResult(result);
            }
        }

        // 5. AwaitOnCompleted
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runnerPromise == null)
            {
                AsyncSTask<TStateMachine, T>.SetStateMachine(ref stateMachine, ref this.runnerPromise);
            }

            awaiter.OnCompleted(this.runnerPromise.MoveNext);
        }

        // 6. AwaitUnsafeOnCompleted
        [SecuritySafeCritical]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runnerPromise == null)
            {
                AsyncSTask<TStateMachine, T>.SetStateMachine(ref stateMachine, ref this.runnerPromise);
            }

            awaiter.UnsafeOnCompleted(this.runnerPromise.MoveNext);
        }

        // 7. Start
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        // 8. SetStateMachine
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            //由于性能问题，这里拒绝使用装箱之后的状态机，所以 nothing happened
        }
    }
```

下面我们来分别讨论其中的成员：

- `runnerPromise` ：持有 `IStateMachineRunnerPromise` 劳动合同的打工仔，是状态机中最重要的成员，它帮助状态机实现异步逻辑。
- `ex` ：任务出现异常时的异常对象。
- `result` ：任务的执行结果。
- `Create()` ：静态 Create 方法，可直接返回状态机本身。在代码执行到 await 语句等待异步方法时调用（这里的异步方法指有 async 关键字，返回类型为 task-like，并且该类型拥有参数为自定义 builder 的 `AsyncMethodBuilder` 特性的方法）。
- `STask<T> Task` ：task-like 对象，这里使用的是我们自己的 STask。在获取它时，我们做了几个判断：
  - 当 `runnerPromise` 对象存在，优先返回它的 `Task` 对象。
  - 否则，当出现异常，返回终止于异常的 `STask` 。（常见于用户提前取消 STask 等待的情况）
  - 若上面条件都不满足，返回结束于现有结果的 `STask` 。（常见于任务在等待前已经完成的情况）
- `SetException(Excetion)` ：当任务遇到异常，该方法被状态机调用。
- `SetResult(T)` ：当任务完成，该方法被状态机调用。
- `AwaitOnCompleted(ref TAwaiter, ref TStateMachine)` ：当该异步方法（被 async 关键字标记）执行到 await 语句，并且 await 后的对象返回的 awaiter.IsCompleted 为 false（表示任务未完成，需等待），将调用该方法。执行到该方法就意味着我们需要处理异步逻辑，因此在这里申请 `runnerPromise` 对象，并且将回调方法注册给 awaiter。
- `AwaitUnsafeOnCompleted(ref TAwaiter, ref TStateMachine)` ：和 `AwaitOnCompleted` 的作用相同，但注册 awaiter 回调时使用的是其 `UnsafeOnCompleted(Action)` 方法。状态机会根据 awaiter 实现的接口来决定调用 `AwaitOnCompleted` 或 `AwaitUnsafeOnCompleted` ，具体的选择策略我们在上文 STaskCompletionSourceCore 小节里有过说明。
- `Start(ref TStateMachine)` ：在 `Create()` 方法后被调用，用于启动状态机，启动后会执行到（第一个）await 语句前。
- `SetStateMachine(IAsyncStateMachine)` ： `builder` 被定义为 struct 类型时，在 `Create()` 方法后被调用。被调用时，系统会传入一个装箱后的状态机实例，目的是满足用户需要缓存状态机实例的需求，但我们用不上，所以该方法体留空。

浏览过 `builder` 的成员后，有小伙伴可能会发现，我们在许多地方都判断了 `runnerPromise` 对象是否存在（是否为空），却不知道为什么 `runnerPromise` 对象有不存在的可能，甚至在看过[官方文档](https://github.com/dotnet/roslyn/blob/main/docs/features/task-types.md)对 `builder` 执行逻辑的陈述后更加迷惑——"After `Start()` returns, the `async` method calls `builder.Task` for the task to return from the async method."，意思是在 `Start()` 方法返回后，状态机会立即获取 `Task` 成员。那么问题来了，在获取 `Task` 时，怎么确定 `runnerPromise` 是否存在呢？

其实在 `Start()` 方法中，我们调用 `stateMachine,MoveNext()` 时，会执行到 await 语句，并且拿到其返回的 awaiter。这时我们会判断该 awaiter 是否已完成，若已完成，则先调用 `builder.SetResult()` 设置任务结果，然后系统才会获取 `Task` （此时 `runnerPromise` 为空）；若没有完成，则先调用 `builder.AwaitUnsafeOnCompleted()` ，再获取 `Task` （此时 `runnerPromise` 不为空）。大伙儿可以看图：

![STaskAsyncMethodBuilder](D:\SaveData\OneDrive\markdown\STask\images\STaskAsyncMethodBuilder.png)

## 小节

这一章我们比较深入地讨论了 STask 的内部实现细节，虽然文章很长，但仍然没有办法将里面的所有细节都讲解到。同学们在最开始阅读不论是 STask 还是 UniTask 的源码时，可以先去感受它的实现思想，暂时将一些细节放一边，等到后面产生疑问时，再针对问题具体分析。对某些模块不清楚时，可以来文章寻找答案，获取可以给你一些启发。当然文章中若有错误或者表达不明确的地方，请务必指出来。

目前为止，我们的 STask 只实现了基本的“异步方法”逻辑，我们使用它的理由，也暂时只有比传统 Task 异步方法性能更好这一个理由——这远远是不够的。因此在下一章，我们将讨论如何扩展 STask，让我们迭代传统打法，优化工具感知度，赋能业务开发流程！~o( =∩ω∩= )



# Unity异步扩展实践（三）——以UniTask库为参考

## 前言

这一章我们来讨论如何让 STask 变得更酷，让我们可以像下面这样实现一些功能：

```c#
await STask.Delay(1000); // 等待1秒

await STask.NextFrame(); // 等待下一帧

await STask.WhenAny(task1, task2); // 等待其中一个任务完成

await STask.WhenAll(task1, task2); // 等待所有任务完成

await SceneManager.LoadSceneAsync("Scene"); // 等待场景加载完毕

await AssetBundle.LoadFromFileAsync("Asset"); // 等待AB包加载完毕
```

上面列举的接口能被大致分为两类：

1. 需要 STask 自己实现的功能。
2. 在 Unity 接口基础上改造，让它能被 await 关键字等待。

后者相对来说比较简单，我们只需实现对应的 awaiter 即可。比如 `SceneManager.LoadSceneAsync()` 方法返回的对象是 `AsyncOperation` ，我们就这样做：

```c#
public static AsyncOperationAwaiter GetAwaiter(this AsyncOperation asyncOperation)
{
	return new AsyncOperationAwaiter(asyncOperation);
}

public struct AsyncOperationAwaiter : ICriticalNotifyCompletion
{
    public AsyncOperationAwaiter(AsyncOperation asyncOperation);
    
    // awaiter logic
    public bool IsCompleted;
    public void GetResult();
    public void OnCompleted(Action continuation);
    public void UnsafeOnCompleted(Action continuation);
}
```

内部实现依靠的是 `AsyncOperation` 对象，完全没有跟 STask 有关的逻辑，因此这一块代码可以随便复制粘贴到任何 Unity 项目。

而第一类功能就稍微复杂一些，具体体现在我们的实现方式上。当我们想在 Unity 中实现“让程序等待一段时间，再执行下面的代码，并且等待时不阻塞当前线程”的功能时，会考虑哪些方法？

- 计时器加回调。
- 在协程中等待，然后执行代码。
- 使用 `Task.Delay()` 接口。

小编最开始想到的是上面三种方法，虽然它们都能达到目的，但各自都有缺点，比如使用麻烦，有不必要的线程切换开销。那如果我们想做到既使用简单，又性能优秀，就不得不介绍 Unity 的 PlayerLoop 系统了。



## PlayerLoopSystem

Unity 的生命周期相信大家都很熟悉了，在2018版本后，Unity 开放了生命周期接口（[先看文档嗷，这是链接](https://docs.unity.cn/2021.1/Documentation/ScriptReference/LowLevel.PlayerLoop.html)），我们可以通过 PlayerLoopSystem 来修改生命周期：

```c#
public class PlayerLoopTest
{
    public struct MyUpdate { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        var mySystem = new PlayerLoopSystem
        {
            subSystemList = new PlayerLoopSystem[]
            {
                new PlayerLoopSystem
                {
                    updateDelegate = CustomUpdate,
                    type = typeof(MyUpdate),
                }
            }
        };

        PlayerLoop.SetPlayerLoop(mySystem);
    }

    private static void CustomUpdate()
    {
        Debug.Log("my update.");
    }
}
```

通过上面代码，我们 Unity 的生命周期事件就只剩下了 `CustomUpdate()` 。如果我们想在原有生命周期事件的基础上进行添加，可以这样做（伪代码）：

```c#
public class PlayerLoopTest
{
    public struct MyUpdate { }
    
    private static void Init()
    {
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopSystem[] copyList = playerLoop.subSystemList.ToArrar();
        int index = FindLoopSystemIndex(copyList, typeof(PlayerLoopType.Update));
        PlayerLoopSystem[] source = copyList[index];
        PlayerLoopSystem[] dest = new PlayerLoopSystem[source.Length + 1];
        Array.Copy(source, 0, dest, 0, source.Length);
        
        PlayerLoopSystem MyUpdate = new PlayerLoopSystem
        {
            type = typeof(MyUpdate),
            updateDelegate = CustomUpdate,
        }
        
        dest[dest.Length - 1] = MyUpdate;
        
        playerLoop.subSystemList = copyList;
        PlayerLoop.SetPlayerLoop(playerLoop);
    }
    
    private static int FindLoopSystemIndex(PlayerLoopSystem[] playerLoopList, Type systemType)
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

    private static void CustomUpdate()
    {
        Debug.Log("my update.");
    }
}
```

通过上面的操作，我们就成功在 Update 生命周期后添加了自定义事件 `CustomUpdate` ，该方法每一帧都会被调用，并打印“my update.”。

回到 STask ，我们将在原有生命周期上添加14处自定义事件（2020.2版本及以上为16处），如下所示：

```c#
//Initialization
//---
//**STaskLoopRunnerYieldInitialization**
//**STaskLoopRunnerInitialization**
//PlayerUpdateTime
//DirectorSampleTime
//AsyncUploadTimeSlicedUpdate  
//SynchronizeInputs  
//SynchronizeState  
//XREarlyUpdate  
//**STaskLoopRunnerLastYieldInitialization**  
//**STaskLoopRunnerLastInitialization**  

//EarlyUpdate  
//---  
//**STaskLoopRunnerYieldEarlyUpdate**  
//**STaskLoopRunnerEarlyUpdate**  
//PollPlayerConnection  
//ProfilerStartFrame  
//GpuTimestamp  
//AnalyticsCoreStatsUpdate  
//UnityWebRequestUpdate  
//ExecuteMainThreadJobs  
//ProcessMouseInWindow  
//ClearIntermediateRenderers  
//ClearLines  
//PresentBeforeUpdate  
//ResetFrameStatsAfterPresent  
//UpdateAsyncReadbackManager  
//UpdateStreamingManager  
//UpdateTextureStreamingManager  
//UpdatePreloading  
//RendererNotifyInvisible  
//PlayerCleanupCachedData  
//UpdateMainGameViewRect  
//UpdateCanvasRectTransform  
//XRUpdate  
//UpdateInputManager  
//ProcessRemoteInput  
//*ScriptRunDelayedStartupFrame*  
//UpdateKinect  
//DeliverIosPlatformEvents  
//TangoUpdate  
//DispatchEventQueueEvents  
//PhysicsResetInterpolatedTransformPosition  
//SpriteAtlasManagerUpdate  
//PerformanceAnalyticsUpdate  
//**STaskLoopRunnerLastYieldEarlyUpdate**  
//**STaskLoopRunnerLastEarlyUpdate**  

//FixedUpdate  
//---  
//**STaskLoopRunnerYieldFixedUpdate**  
//**STaskLoopRunnerFixedUpdate**  
//ClearLines  
//NewInputFixedUpdate  
//DirectorFixedSampleTime  
//AudioFixedUpdate  
//*ScriptRunBehaviourFixedUpdate*  
//DirectorFixedUpdate  
//LegacyFixedAnimationUpdate  
//XRFixedUpdate  
//PhysicsFixedUpdate  
//Physics2DFixedUpdate  
//DirectorFixedUpdatePostPhysics  
//*ScriptRunDelayedFixedFrameRate*  
//**STaskLoopRunnerLastYieldFixedUpdate**  
//**STaskLoopRunnerLastFixedUpdate**  

//PreUpdate  
//---  
//**STaskLoopRunnerYieldPreUpdate**  
//**STaskLoopRunnerPreUpdate**  
//PhysicsUpdate  
//Physics2DUpdate  
//CheckTexFieldInput  
//IMGUISendQueuedEvents  
//NewInputUpdate  
//SendMouseEvents  
//AIUpdate  
//WindUpdate  
//UpdateVideo  
//**STaskLoopRunnerLastYieldPreUpdate**  
//**STaskLoopRunnerLastPreUpdate**  

//Update  
//---  
//**STaskLoopRunnerYieldUpdate**  
//**STaskLoopRunnerUpdate**  
//*ScriptRunBehaviourUpdate*  
//*ScriptRunDelayedDynamicFrameRate*  
//*ScriptRunDelayedTasks*  
//DirectorUpdate  
//**STaskLoopRunnerLastYieldUpdate**  
//**STaskLoopRunnerLastUpdate**  

//PreLateUpdate  
//---  
//**STaskLoopRunnerYieldPreLateUpdate**  
//**STaskLoopRunnerPreLateUpdate**  
//AIUpdatePostScript  
//DirectorUpdateAnimationBegin  
//LegacyAnimationUpdate  
//DirectorUpdateAnimationEnd  
//DirectorDeferredEvaluate  
//EndGraphicsJobsAfterScriptUpdate  
//ParticleSystemBeginUpdateAll  
//ConstraintManagerUpdate  
//*ScriptRunBehaviourLateUpdate*  
//**STaskLoopRunnerLastYieldPreLateUpdate**  
//**STaskLoopRunnerLastPreLateUpdate**  

//PostLateUpdate  
//---  
//**STaskLoopRunnerYieldPostLateUpdate**  
//**STaskLoopRunnerPostLateUpdate**  
//PlayerSendFrameStarted  
//DirectorLateUpdate  
//*ScriptRunDelayedDynamicFrameRate*  
//PhysicsSkinnedClothBeginUpdate  
//UpdateRectTransform  
//UpdateCanvasRectTransform  
//PlayerUpdateCanvases  
//UpdateAudio  
//VFXUpdate  
//ParticleSystemEndUpdateAll  
//EndGraphicsJobsAfterScriptLateUpdate  
//UpdateCustomRenderTextures  
//UpdateAllRenderers  
//EnlightenRuntimeUpdate  
//UpdateAllSkinnedMeshes  
//ProcessWebSendMessages  
//SortingGroupsUpdate  
//UpdateVideoTextures  
//UpdateVideo  
//DirectorRenderImage  
//PlayerEmitCanvasGeometry  
//PhysicsSkinnedClothFinishUpdate  
//FinishFrameRendering  
//BatchModeUpdate  
//PlayerSendFrameComplete  
//UpdateCaptureScreenshot  
//PresentAfterDraw  
//ClearImmediateRenderers  
//PlayerSendFramePostPresent  
//UpdateResolution  
//InputEndFrame  
//TriggerEndOfFrameCallbacks  
//GUIClearEvents  
//ShaderHandleErrors  
//ResetInputAxis  
//ThreadedLoadingDebug  
//ProfilerSynchronizeStats  
//MemoryFrameMaintenance  
//ExecuteGameCenterCallbacks  
//ProfilerEndFrame  
//**STaskLoopRunnerLastYieldPostLateUpdate**  
//**STaskLoopRunnerLastPostLateUpdate**
```

其中前后加了**两颗**星星的为自定义事件，**一颗**星星的是比较重要的自带事件。

聪明的小伙伴可能已经发现，我们每一处插入的事件都分为了两种类型——普通类型和 Yield 类型。

为什么要定义两种类型？

当我们插入自定义事件后，这些事件方法每一帧都会被调用。而 STask 的 PlayerLoopSystem 考虑到通用性，不能仅仅只为了 `STask.Delay()` 这种接口服务，它要支持用户通过 PlayerLoopSystem 的接口向自定义事件中添加需要执行的代码。那么用户提供的代码有两种执行需求：

1. 每一帧执行一次，每一次执行后都判断下一次是否需要继续执行。
2. 只执行一次。

不同的执行需求，实现的方式也是不一样的。STask 的 PlayerLoopSystem 有两个迭代功能的实现小帮手，分别是 PlayerLoopRunner 和 ContinuationQueue .

### PlayerLoopRunner

PlayerLoopRunner 用于处理需要多次迭代的代码。为了判断每次迭代后，下一次是否需要继续迭代，它需要配合接口 IPlayerLoopItem 接口工作。

```c#
/// <summary>
/// PlayerLoopSystem的迭代对象
/// 提供<see cref="MoveNext"/>方法供PlayerLoopSystem迭代，返回 false 时迭代结束
/// （类似IEnumerator）
/// </summary>
/// <seealso href="https://learn.microsoft.com/zh-cn/dotnet/api/system.collections.ienumerator"/>IEnumerator参考
public interface IPlayerLoopItem
{
    bool MoveNext();
}
```

被迭代的对象需要实现该接口，并将待执行代码放进 `MoveNext()` 方法中，当该方法返回 true ，表示下次继续迭代，否则结束迭代。



### ContinuationQueue

ContinuationQueue 用于处理只需要迭代一次的代码。它的实现比较简单，每次迭代后就把回调方法从队列中排除出去。

它与 PlayerLoopRunner 的区别还在于各自使用的线程锁：

1. PlayerLoopRunner：由于不确定IPlayerLoopItem.MoveNext()的执行结果，可能会有比较多Item存在于数组中，迭代一次需要花费较多时间，因此这里用混合多线程锁（Monitor）。
2. ContinuationQueue：迭代对象为委托，由于只执行一次，迭代数组中的元素一般来说比较少，这里用自旋线程锁（SpinLock），且多用在Yield、线程切换上。



### PlayerLoopHelper

STask 的 PlayerLoopSystem 的大部分操作都通过 PlayerLoopHelper 类来实现。当我们需要向 PlaerLoopRunner 或 ContinuationQueue 注册迭代方法时，就可以使用下面两个接口：

```c#
public static void AddAction(PlayerLoopTiming timing, IPlayerLoopItem action)
{
    PlayerLoopRunner runner = runners[(int)timing];
    if (runner == null)
    {
        ThrowInvalidLoopTiming(timing);
    }
    runner.AddAction(action);
}

public static void AddContinuation(PlayerLoopTiming timing, Action continuation)
{
    ContinuationQueue queue = yielders[(int)timing];
    if (queue == null)
    {
        ThrowInvalidLoopTiming(timing);
    }
    queue.Enqueue(continuation);
}
```

与 PlayerLoopSystem 相关的代码比较多，小编就不在这里贴源码了，感兴趣的小伙伴可以前往仓库地址自行翻阅，代码都做了比较详细的注释喔。



## STask.Delay

要实现“让程序在这行代码等待一段时间”的功能，我们需要累加每一帧的间隔时间，并判断从开始到这一帧的时间之和是否达到了预设的等待时间，因此我们需要借助 `PlayerLoopHelper.AddAction` 接口。

```c#
public static STask Delay(int millisecondsDelay, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
{
	TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(millisecondsDelay);
	return new STask(DelayPromise.Create(delayTimeSpan, delayTiming, cancellationToken, out short token), token);
}

private sealed class DelayPromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayPromise>
{
    private static TaskPool<DelayPromise> pool;
    private DelayPromise nextNode;
    public ref DelayPromise NextNode => ref this.nextNode;

    static DelayPromise()
    {
        TaskPool.RegisterSizeGetter(typeof(DelayPromise), () => pool.Size);
    }

    private int initialFrame;
    private float delayTimeSpan;
    private float elapsed;
    private CancellationToken cancellationToken;

    STaskCompletionSourceCore<object> core;

    private DelayPromise() { }

    public static ISTaskSource Create(TimeSpan delayTimeSpan, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
        }

        if(!pool.TryPop(out DelayPromise result))
        {
            result = new DelayPromise();
        }

        result.elapsed = 0.0f;
        result.delayTimeSpan = (float)delayTimeSpan.TotalSeconds;
        result.cancellationToken = cancellationToken;
        result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;

        PlayerLoopHelper.AddAction(timing, result);

        token = result.core.Version;
        return result;
    }

    public void GetResult(short token)
    {
        try
        {
            this.core.GetResult(token);
        }
        finally
        {
            this.TryReturn();
        }
    }

    public STaskStatus GetStatus(short token)
    {
        return this.core.GetStatus(token);
    }

    public STaskStatus UnsafeGetStatus()
    {
        return this.core.UnsafeGetStatus();
    }

    public void OnCompleted(Action<object> continuation, object state, short token)
    {
        this.core.OnCompleted(continuation, state, token);
    }

    public bool MoveNext()
    {
        if(this.cancellationToken.IsCancellationRequested)
        {
            this.core.TrySetCanceled(this.cancellationToken);
            return false;
        }

        if (this.elapsed == 0.0f)//刚开始
        {
            if (this.initialFrame == Time.frameCount)
            {
                return true;
            }
        }

        this.elapsed += Time.deltaTime;
        if (this.elapsed >= this.delayTimeSpan)
        {
            this.core.TrySetResult(null);
            return false;
        }

        return true;
    }

    private bool TryReturn()
    {
        this.core.Reset();
        this.delayTimeSpan = default;
        this.elapsed = default;
        this.cancellationToken = default;
        return pool.TryPush(this);
    }
}
```

重点是 DelayPromise 这个家伙，在它身上我们看到了三个接口：

- ITaskPoolNode：使 DelayPromise 池化。
- ISTaskSource：实现 STask 的相关逻辑。
- IPlayerLoopItem：实现迭代逻辑。

除此之外，我们还有个老朋友 STaskCompletionSourceCore ，它来配合 ISTaskSource 接口的相关方法，处理异步相关的事情，这一点跟 AsyncSTask 类很相似。



## 总结

与上一章的内容相比，STask 的扩展比较简单，唯一麻烦的是前期的准备工作比较多。在理解了扩展的原理后，后面的工作就一泻千里（？了，其他功能的扩展，小伙伴们可以查阅源码来学习。

到此，Unity 异步扩展实践的内容将告一段落。其实小编也没有深入了解全部的 UniTask 代码，它的其他功能，比如 linq 异步，与第三方插件（DoTween）的适配我都还没有了解过，等到以后需要用到的时候再继续吧，哈哈O(∩_∩)O。

最后祝小伙伴们学习愉快，Do what you do best.

