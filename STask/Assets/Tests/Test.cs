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
    }

    public async void TestWhenAll()
    {
        Debug.LogWarning("Test Start");
        var task1 = this.AsyncMethod(100);
        var task2 = this.AsyncMethod(233);
        var task3 = this.AsyncMethod(777);
        var awaitResult = await STask.WhenAll(task1, task2, task3);
        foreach (var result in awaitResult)
        {
            Debug.Log($"time {result}");
        }
        Debug.LogWarning("Test End");
    }

    public async STask<float> AsyncMethod(int milliseconds)
    {
        await STask.Delay(milliseconds);
        return Time.realtimeSinceStartup;
    }
}
