using SFramework.Threading.Tasks.Internal;
using System.Collections;
using System.Collections.Generic;
using SFramework.Threading.Tasks;
using System;
using UnityEngine;

public class Test : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            this.TestWhenAll();
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            this.TestWhenAny();
        }
    }

    public async void TestWhenAll()
    {
        Debug.LogWarning("Test Start");
        var task1 = this.AsyncMethod(100);
        var task2 = this.AsyncMethod(233);
        var task3 = this.AsyncMethod(777);
        var awaitResult = await (task1, task2, task3);
        Debug.Log($"time {awaitResult.Item1}");
        Debug.Log($"time {awaitResult.Item2}");
        Debug.Log($"time {awaitResult.Item3}");
        Debug.LogWarning("Test End");
    }

    public async void TestWhenAny()
    {
        Debug.LogWarning("Test Start");
        var task1 = this.AsyncMethod(888);
        var task2 = this.AsyncMethod(233);
        var task3 = this.AsyncMethod(777);
        var awaitResult = await STask.WhenAny<float>(task2, task1);
        Debug.Log($"left task win? {awaitResult.hasResultLeft}; result is {awaitResult.result}");
        Debug.LogWarning("Test End");
    }

    public async STask<float> AsyncMethod(int milliseconds)
    {
        await STask.Delay(milliseconds);
        return Time.realtimeSinceStartup;
    }
}
