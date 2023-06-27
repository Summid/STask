using System.Collections;
using System.Collections.Generic;

namespace SFramework.Threading.Tasks
{
    public static partial class STaskExtensions
    {
        // shorthand of WhenAll
    
        public static STask.Awaiter GetAwaiter(this STask[] tasks)
        {
            return STask.WhenAll(tasks).GetAwaiter();
        }

        public static STask.Awaiter GetAwaiter(this IEnumerable<STask> tasks)
        {
            return STask.WhenAll(tasks).GetAwaiter();
        }

        public static STask<T[]>.Awaiter GetAwaiter<T>(this STask<T>[] tasks)
        {
            return STask.WhenAll(tasks).GetAwaiter();
        }

        public static STask<T[]>.Awaiter GetAwaiter<T>(this IEnumerable<STask<T>> tasks)
        {
            return STask.WhenAll(tasks).GetAwaiter();
        }

        public static STask<(T1, T2)>.Awaiter GetAwaiter<T1, T2>(this (STask<T1> task1, STask<T2> task2) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2).GetAwaiter();
        }

        public static STask<(T1, T2, T3)>.Awaiter GetAwaiter<T1, T2, T3>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4)>.Awaiter GetAwaiter<T1, T2, T3, T4>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9, STask<T10> task10) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9, STask<T10> task10, STask<T11> task11) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9, STask<T10> task10, STask<T11> task11, STask<T12> task12) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9, STask<T10> task10, STask<T11> task11, STask<T12> task12, STask<T13> task13) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9, STask<T10> task10, STask<T11> task11, STask<T12> task12, STask<T13> task13, STask<T14> task14) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14).GetAwaiter();
        }

        public static STask<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)>.Awaiter GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this (STask<T1> task1, STask<T2> task2, STask<T3> task3, STask<T4> task4, STask<T5> task5, STask<T6> task6, STask<T7> task7, STask<T8> task8, STask<T9> task9, STask<T10> task10, STask<T11> task11, STask<T12> task12, STask<T13> task13, STask<T14> task14, STask<T15> task15) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14, tasks.Item15).GetAwaiter();
        }



        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9, STask task10) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9, STask task10, STask task11) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9, STask task10, STask task11, STask task12) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9, STask task10, STask task11, STask task12, STask task13) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9, STask task10, STask task11, STask task12, STask task13, STask task14) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14).GetAwaiter();
        }


        public static STask.Awaiter GetAwaiter(this (STask task1, STask task2, STask task3, STask task4, STask task5, STask task6, STask task7, STask task8, STask task9, STask task10, STask task11, STask task12, STask task13, STask task14, STask task15) tasks)
        {
            return STask.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14, tasks.Item15).GetAwaiter();
        }
    }
}