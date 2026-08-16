using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public class EnvLoader : MonoBehaviour
{
    public static Dictionary<string, string> env =
        new Dictionary<string, string>();
    private static bool loaded = false;

    void Awake()
    {
        LoadEnv();
    }

    static void LoadEnv()
    {
        if (loaded) return;
        loaded = true;

        string path =
            Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                ".env"
            );

        if (!File.Exists(path))
        {
            Debug.LogError(".env file not found!");
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("#"))
                continue;

            var parts = line.Split('=');

            if (parts.Length == 2)
            {
                env[parts[0]] = parts[1];
            }
        }

        Debug.Log("ENV loaded!");
    }

    public static string Get(string key)
    {
        LoadEnv();
        return env.ContainsKey(key)
            ? env[key]
            : null;
    }
}