using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    private void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    private static UnityMainThreadDispatcher instance = null;

    public static bool Exists()
    {
        return instance != null;
    }

    public static UnityMainThreadDispatcher Instance()
    {
        if (!Exists())
        {
            throw new Exception("UnityMainThreadDispatcher is not initialized.");
        }
        return instance;
    }

    public static void Initialize()
    {
        if (Exists())
        {
            return;
        }

        GameObject obj = new GameObject("UnityMainThreadDispatcher");
        instance = obj.AddComponent<UnityMainThreadDispatcher>();
        DontDestroyOnLoad(obj);
    }
}
