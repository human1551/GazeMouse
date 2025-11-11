/*using System;
using System.Collections.Generic;
using UnityEngine;
using CircularBuffer;
using System.Threading;
using System.Threading.Tasks;

// 必须继承 MonoBehaviour，才能作为组件挂载！
public class EyeLinkTracker : MonoBehaviour, IEyeTracker
{
    // 1. 私有字段（保持原有逻辑）
    private int _disposeCount = 0;
    private readonly object _gazeLock = new object();
    private CircularBuffer<Vector2> _gazes = new CircularBuffer<Vector2>(30);
    private CancellationTokenSource _cts;
    private EyeLinkConnection _connection;

    // 2. 接口成员实现（确保所有成员都完整实现，无遗漏）
    public EyeTracker Type => EyeTracker.EyeLink;
    public float PupilSize { get; private set; }
    public Vector2 Gaze2D
    {
        get
        {
            lock (_gazeLock)
            {
                return _gazes.IsEmpty ? Vector2.zero : _gazes.Back();
            }
        }
    }
    public Vector3 Gaze3D { get; private set; }
    public float SamplingRate { get; set; }
    public float ConfidenceThreshold { get; set; }
    public float Confidence { get; private set; } // 之前补充的接口成员

    // 继承自 IRecorder 的接口成员（根据你的 IRecorder 定义补充，确保无遗漏）
    public string DataFormat { get; set; }
    public string RecordPath { get; set; }
    public RecordStatus RecordStatus { get; set; }
    public AcquisitionStatus AcquisitionStatus { get; set; }

    // 3. MonoBehaviour 生命周期方法（替代原构造函数，Unity 推荐用法）
    private void Awake()
    {
        // 初始化参数（原构造函数的逻辑移到这里）
        SamplingRate = 1000f;
        ConfidenceThreshold = 0.8f;
        _cts = new CancellationTokenSource();
        _connection = new EyeLinkConnection();

        // 可选：设置为单例（避免重复挂载多个眼动仪实例）
        DontDestroyOnLoad(gameObject); // 切换场景不销毁
        var existingInstances = FindObjectsOfType<EyeLinkTracker>();
        if (existingInstances.Length > 1)
        {
            Destroy(gameObject); // 销毁重复实例
        }
    }

    // 4. 连接设备（可通过外部调用，或在 Start 中自动连接）
    public bool Connect(string host = "192.168.1.1", int port = 1000)
    {
        try
        {
            var isConnected = _connection.Connect(host, port);
            if (isConnected)
            {
                // 启动数据采集线程（继承 MonoBehaviour 后，线程逻辑不变）
                Task.Run(() => ReceiveGazeData(_cts.Token));
                Debug.Log("EyeLink 连接成功！");
                return true;
            }
            Debug.LogError("EyeLink 连接失败！");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"EyeLink 连接异常：{ex.Message}");
            return false;
        }
    }

    // 5. 数据采集线程（保持原有逻辑）
    private void ReceiveGazeData(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Thread.Sleep((int)(1000f / SamplingRate)); // 采样间隔

            var data = _connection.GetGazeData();
            if (data == null) continue;

            // 更新接口属性（线程安全）
            lock (_gazeLock)
            {
                Confidence = data.Confidence; // 更新置信度
                if (data.Confidence >= ConfidenceThreshold)
                {
                    PupilSize = data.PupilSize;
                    Gaze3D = data.Gaze3D;
                    _gazes.PushBack(new Vector2(data.GazeX, data.GazeY));
                }
            }
        }
    }

    // 6. 接口方法实现（保持原有逻辑）
    public bool StartRecordAndAcquisite()
    {
        if (_connection?.IsConnected ?? false)
        {
            _connection.StartRecording(RecordPath);
            RecordStatus = RecordStatus.Recording;
            AcquisitionStatus = AcquisitionStatus.Acquiring;
            return true;
        }
        return false;
    }

    public bool StopAcquisiteAndRecord()
    {
        if (_connection?.IsConnected ?? false)
        {
            _connection.StopRecording();
            RecordStatus = RecordStatus.Stopped;
            AcquisitionStatus = AcquisitionStatus.Stopped;
            return true;
        }
        return false;
    }

    public bool ReadDigitalInput(out Dictionary<int, List<double>> dintime, out Dictionary<int, List<int>> dinvalue)
    {
        dintime = new Dictionary<int, List<double>>();
        dinvalue = new Dictionary<int, List<int>>();
        var inputData = _connection.GetDigitalInputs();
        foreach (var (id, times, values) in inputData)
        {
            dintime[id] = times;
            dinvalue[id] = values;
        }
        return true;
    }

    // 7. MonoBehaviour 资源释放（替代原 Dispose，Unity 推荐在 OnDestroy 中释放）
    private void OnDestroy()
    {
        // 取消线程、断开连接、释放资源
        _cts?.Cancel();
        _connection?.Disconnect();
        _cts?.Dispose();
        _connection = null;
        _gazes = null;
        Debug.Log("EyeLink 资源已释放！");
    }
}

// 模拟 EyeLink 设备连接和数据结构（保持不变）
internal class EyeLinkConnection
{
    public bool IsConnected { get; private set; }

    public bool Connect(string host, int port)
    {
        IsConnected = true; // 实际项目替换为真实连接逻辑
        return true;
    }

    public void Disconnect() => IsConnected = false;

    public void StartRecording(string path) => Debug.Log($"开始记录到：{path}");

    public void StopRecording() => Debug.Log("停止记录");

    public GazeData GetGazeData()
    {
        // 模拟数据（实际替换为 EyeLink SDK 数据读取）
        return new GazeData
        {
            Confidence = UnityEngine.Random.Range(0.7f, 1f),
            PupilSize = UnityEngine.Random.Range(2f, 6f),
            GazeX = UnityEngine.Random.Range(0f, 1920f), // 假设屏幕宽度 1920
            GazeY = UnityEngine.Random.Range(0f, 1080f), // 假设屏幕高度 1080
            Gaze3D = new Vector3(0, 0, 1)
        };
    }

    public IEnumerable<(int id, List<double> times, List<int> values)> GetDigitalInputs()
    {
        return new List<(int, List<double>, List<int>)>();
    }
}

internal class GazeData
{
    public float Confidence { get; set; }
    public float PupilSize { get; set; }
    public float GazeX { get; set; }
    public float GazeY { get; set; }
    public Vector3 Gaze3D { get; set; }
}

// 必要的枚举定义（确保你的项目中已有这些枚举，无则添加）
public enum EyeTracker { EyeLink, Tobii, PupilLabs }
public enum RecordStatus { Stopped, Recording, Paused }
public enum AcquisitionStatus { Stopped, Acquiring, Paused }

// IEyeTracker 接口定义（确保和你的接口完全一致，无遗漏）
public interface IEyeTracker : IRecorder
{
    EyeTracker Type { get; }
    float PupilSize { get; }
    Vector2 Gaze2D { get; }
    Vector3 Gaze3D { get; }
    float SamplingRate { get; set; }
    float ConfidenceThreshold { get; set; }
    float Confidence { get; } // 之前补充的成员
}

// IRecorder 接口定义（根据你的实际定义补充，确保无遗漏）
public interface IRecorder
{
    string DataFormat { get; set; }
    string RecordPath { get; set; }
    RecordStatus RecordStatus { get; set; }
    AcquisitionStatus AcquisitionStatus { get; set; }
    bool StartRecordAndAcquisite();
    bool StopAcquisiteAndRecord();
    bool ReadDigitalInput(out Dictionary<int, List<double>> dintime, out Dictionary<int, List<int>> dinvalue);
}*/