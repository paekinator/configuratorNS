using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Confirms browser IndexedDB writes before the UI says saved.</summary>
public sealed class LocalSavePersistence : MonoBehaviour
{
    static LocalSavePersistence _instance;
    readonly Queue<Action<string>> _pending = new Queue<Action<string>>();
    Action<string> _active;
    float _deadline;
    bool _timedOut;
    const string TimeoutError = "Browser storage did not confirm the save. Keep this tab open and copy the piece code as a backup.";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void NeoSyncLocalSaves(string receiver);
#endif

    public static void Flush(Action<string> completed)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_instance == null)
        {
            var host = new GameObject("LocalSavePersistence");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<LocalSavePersistence>();
        }
        if (_instance._timedOut)
        {
            completed?.Invoke(TimeoutError);
            return;
        }
        _instance._pending.Enqueue(completed ?? (_ => { }));
        _instance.StartNext();
#else
        completed?.Invoke(null);
#endif
    }

    void StartNext()
    {
        if (_active != null || _pending.Count == 0) return;
        _active = _pending.Dequeue();
        _deadline = Time.realtimeSinceStartup + 20f;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { NeoSyncLocalSaves(gameObject.name); }
        catch (Exception e) { OnStorageFlushed(e.Message); }
#endif
    }

    // Called from the browser after its asynchronous filesystem flush.
    public void OnStorageFlushed(string error)
    {
        var callback = _active;
        _active = null;
        _timedOut = false;
        try { callback?.Invoke(string.IsNullOrEmpty(error) ? null : error); }
        finally { StartNext(); }
    }

    void Update()
    {
        if (_active == null || Time.realtimeSinceStartup < _deadline) return;
        // Keep the slot occupied until JS responds so a late callback cannot
        // acknowledge a different save. Tell this caller persistence is unknown.
        var callback = _active;
        _active = _ => { };
        _timedOut = true;
        _deadline = float.PositiveInfinity;
        try { callback(TimeoutError); }
        finally
        {
            while (_pending.Count > 0)
            {
                try { _pending.Dequeue()(TimeoutError); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
