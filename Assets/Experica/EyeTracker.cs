/*
EyeTracker.cs is part of the Experica.
Copyright (c) 2016 Li Alex Zhang and Contributors

Permission is hereby granted, free of charge, to any person obtaining a 
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the 
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included 
in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF 
OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using CircularBuffer;

namespace Experica
{
    public enum EyeTracker
    {
        EyeLink,
        Tobii,
        PupilLabs_Core
    }

    /// <summary>
    /// 置信度状态枚举
    /// </summary>
    public enum ConfidenceState
    {
        Normal,
        Low,
        High
    }

    public interface IEyeTracker : IRecorder  //public interface IRecorder : IRecord, ISignal 在IRecord.cs 里
    {
        EyeTracker Type { get; }
        float PupilSize { get; }
        Vector2 Gaze2D { get; }
        Vector3 Gaze3D { get; }
        float SamplingRate { get; set; }
        float ConfidenceThreshold { get; set; }
        //在core开发手册里没找着Blink……或许检测置信度的下降和上升？间隔？下降时间？
        float Confidence { get; }
        


    public class PupilLabsCore : IEyeTracker
        {
            int disposecount = 0;
            readonly object api = new();
            readonly object gazelock = new();
            RequestSocket req_socket;
            string sub_port;
            SubscriberSocket sub_socket;
            CancellationTokenSource cts = new();
            CircularBuffer<Vector2> gazes = new(30);

            private float _lastConfidence = -1f; 
            private bool? _isRising = null; 

            private float _currentConfidence; 
            public float Confidence => _currentConfidence;
            // 添加左右眼置信度跟踪
            private float _leftEyeLastConfidence = -1f;
            private float _rightEyeLastConfidence = -1f;
            private bool _leftHasFallenBelowThreshold = false;
            private bool _rightHasFallenBelowThreshold = false;
            private bool _leftBlinkTriggered = false;
            private bool _rightBlinkTriggered = false;

            public event Action LeftEyeBlinked;
            public event Action RightEyeBlinked;
            private DateTime _lastLeftBlinkTime = DateTime.MinValue;
            private DateTime _lastRightBlinkTime = DateTime.MinValue;
            private DateTime? _leftBlinkTime = null;
            private DateTime? _rightBlinkTime = null;
            private readonly TimeSpan _blinkIntervalMin = TimeSpan.FromMilliseconds(100);
            [SerializeField] private readonly TimeSpan _blinkIntervalThreshold = TimeSpan.FromMilliseconds(100); // 双眼眨眼间隔阈值
            //private readonly TimeSpan _blinkSyncThreshold = TimeSpan.FromSeconds(1);
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (1 == Interlocked.Exchange(ref disposecount, 1))
                {
                    return;
                }
                Disconnect();
            }

            public static PupilLabsCore TryGetPupilLabsCore(string host = "localhost", int port = 50020, float fs = 200, float confthreshold = 0.9f)
            {
                var t = new PupilLabsCore(fs, confthreshold);
                if (t.Connect(host, port)) { return t; }
                else
                {
                    Debug.LogWarning("Can't Connect to PupilLabs Core, return Null.");
                    return null;
                }
            }

            public PupilLabsCore(float fs = 200, float confthreshold = 0.9f)
            {
                SamplingRate = fs;
                ConfidenceThreshold = confthreshold;
            }

            ~PupilLabsCore()
            {
                Dispose(false);
            }

            public bool StartRecordAndAcquisite()
            {
                req_socket.SendFrame(Encoding.UTF8.GetBytes("R"));
                req_socket.ReceiveFrameString();
                return true;
            }

            public bool StopAcquisiteAndRecord()
            {
                req_socket.SendFrame(Encoding.UTF8.GetBytes("r"));
                req_socket.ReceiveFrameString();
                return true;
            }

            public bool Connect(string host = "localhost", int port = 50020)
            {
                req_socket = new RequestSocket($"tcp://{host}:{port}");
                req_socket.TrySendFrame("SUB_PORT");
                if (req_socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(500), out sub_port))
                {
                    sub_socket = new SubscriberSocket($"tcp://{host}:{sub_port}");

                    sub_socket.Subscribe("surfaces.");
                    sub_socket.Subscribe("pupil.1.2d");  // 左眼
                    sub_socket.Subscribe("pupil.0.2d");  // 右眼
                    Task.Run(() => receivegaze(cts.Token));
                    return true;
                }
                req_socket.Dispose();
                req_socket = null;
                return false;
            }

            public void Disconnect()
            {
                if (cts != null)
                {
                    cts.Cancel();
                    cts = null;
                }
                sub_socket?.Dispose();
                sub_socket = null;
                req_socket?.Dispose();
                req_socket = null;
                NetMQConfig.Cleanup(false);
            }

            public bool ReadDigitalInput(out Dictionary<int, List<double>> dintime, out Dictionary<int, List<int>> dinvalue)
            {
                throw new NotImplementedException();
            }

            public EyeTracker Type => EyeTracker.PupilLabs_Core;

            public float PupilSize => throw new NotImplementedException();

            public Vector2 Gaze2D
            {
                get
                {
                    lock (gazelock) { return gazes.IsEmpty ? Vector2.zero : gazes.Back(); }
                }
            }

            // 置信度阈值
            private const float FALL_THRESHOLD = 0.3f;   // 下降阈值：30%
            private const float RISE_THRESHOLD = 0.7f;   // 上升阈值：70%
            private bool _hasFallenBelowThreshold = false;  // 标记是否已下降到30%以下
            private bool _triggered = false;  // 标记是否已触发过点击（避免重复触发）

            /// <summary>
            /// 当置信度上升时触发
            /// </summary>
            /// <param name="current">当前置信度</param>
            /// <param name="previous">上一次置信度</param>
            private void OnConfidenceRising(float current, float previous)
            {
                Debug.Log($"置信度上升: {previous:F2} → {current:F2}");
            }

            /// <summary>
            /// 当置信度下降时触发
            /// </summary>
            /// <param name="current">当前置信度</param>
            /// <param name="previous">上一次置信度</param>
            private void OnConfidenceFalling(float current, float previous)
            {
                Debug.Log($"置信度下降: {previous:F2} → {current:F2}");
            }
            
            /*private void CheckConfidenceSequence(float currentConfidence)
            {
                // 第一步：检测是否下降到30%以下
                if (currentConfidence < FALL_THRESHOLD)
                {
                    _hasFallenBelowThreshold = true;  // 标记已进入“下降状态”
                    _triggered = false;  // 重置触发标记（确保下次满足条件时能触发）
                }
                // 第二步：如果已下降到30%以下，再检测是否上升到70%以上
                else if (_hasFallenBelowThreshold && currentConfidence > RISE_THRESHOLD && !_triggered)
                {
                    // 满足完整条件，触发鼠标点击
                    TriggerMouseClick();

                    // 标记已触发，避免重复点击
                    _triggered = true;
                    // 重置状态，准备下一次检测
                    _hasFallenBelowThreshold = false;
                }
            }*/

            //检测两次“鼠标点击”事件的间隔，小于指定则判定为一次双击
            //private DateTime _lastClickTime = DateTime.MinValue;
            //private readonly TimeSpan _doubleClickInterval = TimeSpan.FromMilliseconds(500); // 双击间隔时间（可调整）
            /*private void CheckDoubleClick()
            {
                if (DateTime.Now - _lastClickTime < _doubleClickInterval)
                {
                    // 在指定时间内再次点击，判定为双击
                    Debug.Log("双击");
                    // 可在此处添加双击事件处理逻辑
                }
                else
                {
                    // 单击
                    Debug.Log("单击");
                    // 可在此处添加单击事件处理逻辑
                }
                // 更新最后一次点击时间
                _lastClickTime = DateTime.Now;
            }*/

            //左键。不检测双眼同时闭上（减少误操作)，只分别检测两只眼睛的置信度变化
            //base_data里的pupil.0和pupil.1数据……
            private void HandleEyeData(Dictionary<string, object> data, bool isLeftEye)
            {
                if (data.ContainsKey("confidence"))
                {
                    // 提取当前眼的置信度（瞳孔数据中的置信度）
                    float currentConfidence = (float)(double)data["confidence"];

                    if (isLeftEye)
                    {
                        // 左眼处理：检查眨眼模式
                        if (_leftEyeLastConfidence >= 0)
                        {
                            CheckBlink(
                                currentConfidence,
                                _leftEyeLastConfidence,
                                ref _leftHasFallenBelowThreshold,
                                ref _leftBlinkTriggered,
                                isLeftEye: true
                            );
                        }
                        _leftEyeLastConfidence = currentConfidence;
                    }
                    else
                    {
                        // 右眼处理：检查眨眼模式
                        if (_rightEyeLastConfidence >= 0)
                        {
                            CheckBlink(
                                currentConfidence,
                                _rightEyeLastConfidence,
                                ref _rightHasFallenBelowThreshold,
                                ref _rightBlinkTriggered,
                                isLeftEye: false
                            );
                        }
                        _rightEyeLastConfidence = currentConfidence;
                    }
                }
            }



            /*private void TriggerMouseClick()
            {
                WindowsInputSimulator.SimulateLeftClick();
                Debug.Log("触发鼠标点击：置信度先低于30%后高于70%");
            }*/

            private void CheckBlink(float currentConfidence, float lastConfidence,
                          ref bool hasFallenBelowThreshold, ref bool blinkTriggered, bool isLeftEye)
            {
                // 检测置信度是否下降到阈值以下（可能开始眨眼）
                if (currentConfidence < FALL_THRESHOLD)
                {
                    hasFallenBelowThreshold = true;
                    blinkTriggered = false;
                }
                // 若已下降到阈值以下，且当前上升到阈值以上（眨眼结束）
                else if (hasFallenBelowThreshold && currentConfidence > RISE_THRESHOLD && !blinkTriggered)
                {
                    // 记录当前眨眼时间
                    var currentBlinkTime = DateTime.Now;
                    // 检查是否与另一只眼的眨眼时间重叠
                    bool isSimultaneousBlink = false;
                    
                    if (isLeftEye)
                    {
                        _leftBlinkTime = currentBlinkTime;
                        // 检查右眼是否在阈值时间内也眨了眼
                        if (_rightBlinkTime.HasValue && currentBlinkTime - _rightBlinkTime.Value <= _blinkIntervalThreshold)
                        {
                            isSimultaneousBlink = true;
                            _rightBlinkTime = null; // 重置右眼眨眼时间，避免重复检测
                        }
                    }
                    else
                    {
                        _rightBlinkTime = currentBlinkTime;
                        // 检查左眼是否在阈值时间内也眨了眼
                        if (_leftBlinkTime.HasValue && currentBlinkTime - _leftBlinkTime.Value <= _blinkIntervalThreshold)
                        {
                            isSimultaneousBlink = true;
                            _leftBlinkTime = null; // 重置左眼眨眼时间，避免重复检测
                        }
                    }

                    // 只有非同时眨眼才触发鼠标事件
                    if (!isSimultaneousBlink)
                    {
                        var now = DateTime.Now;
                        if (isLeftEye && now - _lastLeftBlinkTime >= _blinkIntervalMin)
                        {
                            LeftEyeBlinked();
                            _lastLeftBlinkTime = now;
                            Debug.Log("左眼眨眼 - 触发鼠标左键点击");
                        }
                        else
                        {
                            RightEyeBlinked();
                            _lastRightBlinkTime = now;
                            Debug.Log("右眼眨眼 - 触发鼠标右键点击");
                        }
                    }
                    else
                    {
                        Debug.Log($"双眼同步眨眼（间隔：{Math.Round((currentBlinkTime - (isLeftEye ? _rightBlinkTime.Value : _leftBlinkTime.Value)).TotalMilliseconds)}ms）- 不触发操作");
                    }

                    blinkTriggered = true;
                    hasFallenBelowThreshold = false;
                }
            }
            void receivegaze(CancellationToken token)
            {
                var msg = new NetMQMessage();
                string topic;
                byte[] payload;
                Dictionary<string, object> payloadDict;
                Dictionary<object, object> gazeDict;
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(Mathf.FloorToInt(1000f / SamplingRate));
                    if (sub_socket.TryReceiveMultipartMessage(ref msg) && msg.FrameCount == 2)
                    {
                        if (msg.FrameCount == 2)
                        {
                            topic = msg[0].ConvertToString();
                            payload = msg[1].ToByteArray();
                            payloadDict = MsgPack.DeserializeMsgPack<Dictionary<string, object>>(payload);

                            // 处理瞳孔数据，区分左右眼
                            if (topic.StartsWith("pupil.1.2d"))  // 左眼
                            {
                                HandleEyeData(payloadDict, true);
                            }
                            else if (topic.StartsWith("pupil.0.2d"))  // 右眼
                            {
                                HandleEyeData(payloadDict, false);
                            }
                            else if (payloadDict.ContainsKey("gaze_on_surfaces"))
                            {
                                foreach (var gazeObj in payloadDict["gaze_on_surfaces"].AsList())
                                {
                                    gazeDict = gazeObj as Dictionary<object, object>;
                                    if (gazeDict.ContainsKey("norm_pos") && gazeDict.ContainsKey("confidence"))
                                    {
                                        // 提取并更新当前置信度
                                        _currentConfidence = (float)(double)gazeDict["confidence"];
                                        // 提取当前置信度
                                        var currentConfidence = (float)(double)gazeDict["confidence"];
                                        // 监测置信度变化（首次获取时不判断，仅记录）
                                        if (_lastConfidence >= 0) // 确保历史值有效
                                        {
                                            // 计算变化量（可根据需求添加阈值，过滤微小波动）
                                            var delta = currentConfidence - _lastConfidence;
                                            const float MIN_CHANGE = 0.05f; // 最小变化阈值（例如5%）

                                            if (delta > MIN_CHANGE)
                                            {
                                                // 置信度上升
                                                _isRising = true;
                                                OnConfidenceRising(currentConfidence, _lastConfidence); // 触发上升事件
                                            }
                                            else if (delta < -MIN_CHANGE)
                                            {
                                                // 置信度下降
                                                _isRising = false;
                                                OnConfidenceFalling(currentConfidence, _lastConfidence); // 触发下降事件
                                            }
                                            // 变化小于阈值时不处理（视为不变）
                                        }

                                        // 更新历史置信度
                                        _lastConfidence = currentConfidence;
                                        //这里    

                                        //过滤低置信度数据并存储凝视点
                                        var confidence = (double)gazeDict["confidence"];
                                        if (confidence < ConfidenceThreshold) { continue; }
                                        var normPosList = gazeDict["norm_pos"].AsList();
                                        var gaze = new Vector2(Convert.ToSingle(normPosList[0]), Convert.ToSingle(normPosList[1]));
                                        lock (gazelock)
                                        {
                                            gazes.PushBack(gaze);
                                        }
                                    }
                                }

                            }
                        }
                    }
                }
            }

            public string DataFormat { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string RecordPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public RecordStatus RecordStatus { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public AcquisitionStatus AcquisitionStatus { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public Vector3 Gaze3D => throw new NotImplementedException();

            public float SamplingRate { get; set; }
            public float ConfidenceThreshold { get; set; }

        }
    }
}