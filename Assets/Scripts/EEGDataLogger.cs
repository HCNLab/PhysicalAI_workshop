using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Gtec.Bandpower;
using static Gtec.Chain.Common.Templates.DataAcquisitionUnit.DataAcquisitionUnit;

/// <summary>
/// EEG Data Logger for g.tec Unicorn BCI Core-8
/// SETUP: Add this script as a CHILD of the Device prefab
/// </summary>
public class EEGDataLogger : MonoBehaviour
{
    [Header("Logging Settings")]
    public string logFolderName = "EEGData";
    public bool logRawEEG = true;
    public bool logBandpower = true;
    public string filePrefix = "session";
    
    [Header("Auto Start")]
    public bool autoStartOnConnect = true;
    
    [Header("Status")]
    public bool isLogging = false;
    public bool isConnected = false;
    public int samplesLogged = 0;
    public int bandpowerSamplesLogged = 0;
    public int eventsLogged = 0;
    
    private Device _device;
    private StreamWriter rawEEGWriter;
    private StreamWriter bandpowerWriter;
    private StreamWriter eventWriter;
    private string sessionFolder;
    private DateTime sessionStartTime;
    
    void Start()
    {
        try { _device = GetComponentInParent<Device>(); }
        catch { _device = null; }
        
        if (_device != null)
        {
            AttachEvents();
            Debug.Log("[EEGDataLogger] Connected to Device.");
        }
        else
        {
            Debug.LogError("[EEGDataLogger] Device not found! Must be child of Device prefab.");
        }
    }
    
    void AttachEvents()
    {
        if (_device != null)
        {
            _device.OnDeviceStateChanged.AddListener(OnDeviceStateChanged);
            _device.OnMeanBandpowerAvailable.AddListener(OnMeanBandpowerReceived);
            _device.OnEEGDataAvailable.AddListener(OnEEGDataReceived);
        }
    }
    
    void RemoveEvents()
    {
        if (_device != null)
        {
            _device.OnDeviceStateChanged.RemoveListener(OnDeviceStateChanged);
            _device.OnMeanBandpowerAvailable.RemoveListener(OnMeanBandpowerReceived);
            _device.OnEEGDataAvailable.RemoveListener(OnEEGDataReceived);
        }
    }
    
    void OnDeviceStateChanged(States state)
    {
        Debug.Log($"[EEGDataLogger] State: {state}");
        
        if (state == States.Connected)
        {
            isConnected = true;
            if (autoStartOnConnect && !isLogging) StartLogging();
        }
        else if (state == States.Disconnected)
        {
            isConnected = false;
            if (isLogging) StopLogging();
        }
    }
    
    void OnEEGDataReceived(Rawdata rawData)
    {
        if (!isLogging || rawEEGWriter == null || rawData == null) return;
        
        try
        {
            float[,] eegData = rawData.Data;
            int samples = eegData.GetLength(0);
            int channels = eegData.GetLength(1);
            
            for (int s = 0; s < samples; s++)
            {
                double relativeTime = (DateTime.Now - sessionStartTime).TotalMilliseconds;
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                
                StringBuilder sb = new StringBuilder();
                sb.Append(timestamp).Append(",").Append(relativeTime.ToString("F2"));
                
                for (int ch = 0; ch < channels && ch < 8; ch++)
                {
                    sb.Append(",").Append(eegData[s, ch].ToString("F6"));
                }
                
                rawEEGWriter.WriteLine(sb.ToString());
                samplesLogged++;
            }
            
            if (samplesLogged % 100 == 0) rawEEGWriter.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EEGDataLogger] EEG Error: {ex.Message}");
        }
    }
    
    void OnMeanBandpowerReceived(Dictionary<string, double> bp)
    {
        if (!isLogging || bandpowerWriter == null || bp == null) return;
        
        try
        {
            double relativeTime = (DateTime.Now - sessionStartTime).TotalMilliseconds;
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            
            double delta = bp.ContainsKey("delta") ? bp["delta"] : 0;
            double theta = bp.ContainsKey("theta") ? bp["theta"] : 0;
            double alpha = bp.ContainsKey("alpha") ? bp["alpha"] : 0;
            double betaLow = bp.ContainsKey("beta-low") ? bp["beta-low"] : 0;
            double betaMid = bp.ContainsKey("beta-mid") ? bp["beta-mid"] : 0;
            double betaHigh = bp.ContainsKey("beta-high") ? bp["beta-high"] : 0;
            double gamma = bp.ContainsKey("gamma") ? bp["gamma"] : 0;
            
            bandpowerWriter.WriteLine($"{timestamp},{relativeTime:F2},{delta:F6},{theta:F6},{alpha:F6},{betaLow:F6},{betaMid:F6},{betaHigh:F6},{gamma:F6}");
            bandpowerWriter.Flush();
            bandpowerSamplesLogged++;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EEGDataLogger] Bandpower Error: {ex.Message}");
        }
    }
    
    [ContextMenu("Start Logging")]
    public void StartLogging()
    {
        if (isLogging) return;
        
        sessionStartTime = DateTime.Now;
        string ts = sessionStartTime.ToString("yyyyMMdd_HHmmss");
        
        string basePath = Path.Combine(Application.streamingAssetsPath, logFolderName);
        sessionFolder = Path.Combine(basePath, $"{filePrefix}_{ts}");
        if (!Directory.Exists(sessionFolder)) Directory.CreateDirectory(sessionFolder);
        
        if (logRawEEG)
        {
            rawEEGWriter = new StreamWriter(Path.Combine(sessionFolder, "raw_eeg.csv"), false, Encoding.UTF8);
            rawEEGWriter.WriteLine("Timestamp,RelativeTime_ms,Ch1,Ch2,Ch3,Ch4,Ch5,Ch6,Ch7,Ch8");
            rawEEGWriter.Flush();
        }
        
        if (logBandpower)
        {
            bandpowerWriter = new StreamWriter(Path.Combine(sessionFolder, "bandpower.csv"), false, Encoding.UTF8);
            bandpowerWriter.WriteLine("Timestamp,RelativeTime_ms,Delta,Theta,Alpha,BetaLow,BetaMid,BetaHigh,Gamma");
            bandpowerWriter.Flush();
        }
        
        eventWriter = new StreamWriter(Path.Combine(sessionFolder, "events.csv"), false, Encoding.UTF8);
        eventWriter.WriteLine("Timestamp,RelativeTime_ms,EventType,EventData");
        eventWriter.Flush();
        
        isLogging = true;
        samplesLogged = 0;
        bandpowerSamplesLogged = 0;
        eventsLogged = 0;
        
        Debug.Log($"[EEGDataLogger] Started: {sessionFolder}");
        LogEvent("SESSION_START", "");
    }
    
    [ContextMenu("Stop Logging")]
    public void StopLogging()
    {
        if (!isLogging) return;
        
        LogEvent("SESSION_END", $"EEG:{samplesLogged} BP:{bandpowerSamplesLogged}");
        isLogging = false;
        
        if (rawEEGWriter != null) { rawEEGWriter.Close(); rawEEGWriter = null; }
        if (bandpowerWriter != null) { bandpowerWriter.Close(); bandpowerWriter = null; }
        if (eventWriter != null) { eventWriter.Close(); eventWriter = null; }
        
        Debug.Log($"[EEGDataLogger] Stopped. EEG:{samplesLogged} BP:{bandpowerSamplesLogged}");
    }
    
    public void LogEvent(string eventType, string eventData = "")
    {
        if (eventWriter == null) return;
        
        double relativeTime = isLogging ? (DateTime.Now - sessionStartTime).TotalMilliseconds : 0;
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        eventWriter.WriteLine($"{timestamp},{relativeTime:F2},{eventType},{eventData.Replace(",", ";")}");
        eventWriter.Flush();
        eventsLogged++;
    }
    
    // Experiment event markers
    public void LogRobotActionStart(string action) => LogEvent("ROBOT_START", action);
    public void LogRobotActionEnd(string action) => LogEvent("ROBOT_END", action);
    public void LogAssemblyEvent(string evt) => LogEvent("ASSEMBLY", evt);
    public void LogUserInteraction(string interaction) => LogEvent("USER", interaction);
    
    void OnDestroy() { RemoveEvents(); if (isLogging) StopLogging(); }
    void OnApplicationQuit() { RemoveEvents(); if (isLogging) StopLogging(); }
}
