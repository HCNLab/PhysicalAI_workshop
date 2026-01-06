using UnityEngine;
using System;
using System.Collections.Generic;
using LSL;

/// <summary>
/// LSL Manager for EEG Integration
/// Provides LSL inlet (receive EEG) and outlet (send markers)
/// Supports multiple EEG devices: Unicorn, BioSemi, etc.
/// </summary>
public class LSLManager : MonoBehaviour
{
    [Header("Stream Settings")]
    [Tooltip("Type of LSL stream to look for (e.g., 'EEG', 'Markers')")]
    public string eegStreamType = "EEG";
    
    [Tooltip("Name pattern to filter streams (leave empty for any)")]
    public string streamNameFilter = "";
    
    [Header("Marker Outlet")]
    [Tooltip("Name for the marker outlet stream")]
    public string markerStreamName = "UnityMarkers";
    
    [Header("Data Logging")]
    public EEGDataLogger eegDataLogger;
    
    [Header("Status")]
    public bool isReceivingEEG = false;
    public bool isMarkerOutletReady = false;
    public int samplesReceived = 0;
    public string connectedStreamName = "";
    
    // LSL objects
    private StreamInlet eegInlet;
    private StreamOutlet markerOutlet;
    private StreamInfo markerInfo;
    
    // EEG data buffer
    private float[] eegSample;
    private int channelCount = 0;
    private double lastTimestamp = 0;
    
    void Start()
    {
        // Auto-find EEGDataLogger
        if (eegDataLogger == null)
            eegDataLogger = FindFirstObjectByType<EEGDataLogger>();
        
        // Create marker outlet
        CreateMarkerOutlet();
        
        // Debug.Log("[LSLManager] Initialized. Call SearchAndConnectEEGStream() to connect.");
    }
    
    void Update()
    {
        // Pull EEG data if connected
        if (eegInlet != null && isReceivingEEG)
        {
            PullEEGData();
        }
    }
    
    /// <summary>
    /// Search for available LSL streams of EEG type
    /// </summary>
    public string[] GetAvailableStreams()
    {
        StreamInfo[] streams = LSL.LSL.resolve_stream("type", eegStreamType, 0, 2.0);
        
        List<string> streamNames = new List<string>();
        foreach (var stream in streams)
        {
            string name = stream.name();
            if (string.IsNullOrEmpty(streamNameFilter) || name.Contains(streamNameFilter))
            {
                streamNames.Add($"{name} ({stream.channel_count()} ch, {stream.nominal_srate()} Hz)");
            }
        }
        
        // Debug.Log($"[LSLManager] Found {streamNames.Count} EEG streams");
        return streamNames.ToArray();
    }
    
    /// <summary>
    /// Connect to the first available EEG stream
    /// </summary>
    [ContextMenu("Search and Connect EEG Stream")]
    public void SearchAndConnectEEGStream()
    {
        try
        {
            // Debug.Log($"[LSLManager] Searching for '{eegStreamType}' streams...");
            
            StreamInfo[] streams = LSL.LSL.resolve_stream("type", eegStreamType, 1, 5.0);
            
            if (streams.Length == 0)
            {
                Debug.LogWarning("[LSLManager] No EEG streams found!");
                return;
            }
            
            // Connect to first stream (or filtered)
            StreamInfo selectedStream = null;
            foreach (var stream in streams)
            {
                if (string.IsNullOrEmpty(streamNameFilter) || stream.name().Contains(streamNameFilter))
                {
                    selectedStream = stream;
                    break;
                }
            }
            
            if (selectedStream == null)
            {
                Debug.LogWarning($"[LSLManager] No stream matching filter '{streamNameFilter}'");
                return;
            }
            
            ConnectToStream(selectedStream);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LSLManager] Error connecting: {ex.Message}");
        }
    }
    
    void ConnectToStream(StreamInfo streamInfo)
    {
        // Close existing connection
        DisconnectEEGStream();
        
        // Create inlet
        eegInlet = new StreamInlet(streamInfo);
        channelCount = streamInfo.channel_count();
        eegSample = new float[channelCount];
        connectedStreamName = streamInfo.name();
        
        isReceivingEEG = true;
        
        Debug.Log($"[LSLManager] Connected to: {connectedStreamName} ({channelCount} channels)");
        
        // Send marker
        SendMarkerText("LSL_CONNECTED", connectedStreamName);
    }
    
    /// <summary>
    /// Disconnect from current EEG stream
    /// </summary>
    [ContextMenu("Disconnect EEG Stream")]
    public void DisconnectEEGStream()
    {
        if (eegInlet != null)
        {
            isReceivingEEG = false;
            eegInlet.close_stream();
            eegInlet = null;
            connectedStreamName = "";
            Debug.Log("[LSLManager] Disconnected from EEG stream");
        }
    }
    
    void PullEEGData()
    {
        try
        {
            double timestamp = eegInlet.pull_sample(eegSample, 0.0);  // Non-blocking
            
            if (timestamp > 0 && timestamp != lastTimestamp)
            {
                lastTimestamp = timestamp;
                samplesReceived++;
                
                // Forward to EEGDataLogger if available
                if (eegDataLogger != null && eegDataLogger.isLogging)
                {
                    // Log raw EEG data - convert float[] to format logger expects
                    LogEEGSample(eegSample, timestamp);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LSLManager] Pull error: {ex.Message}");
            isReceivingEEG = false;
        }
    }
    
    void LogEEGSample(float[] sample, double timestamp)
    {
        // Log to EEGDataLogger as event with data
        string channelData = string.Join(",", sample);
        eegDataLogger.LogEvent("LSL_EEG", channelData);
    }
    
    // ===== Marker Outlet =====
    
    void CreateMarkerOutlet()
    {
        try
        {
            // Change to int32 for better compatibility with OpenViBE
            markerInfo = new StreamInfo(markerStreamName, "Markers", 1, 0, channel_format_t.cf_int32, "UnityMarkers_001");
            markerOutlet = new StreamOutlet(markerInfo);
            isMarkerOutletReady = true;
            Debug.Log($"[LSLManager] Marker outlet created: {markerStreamName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LSLManager] Failed to create marker outlet: {ex.Message}");
        }
    }
    
    // OpenViBE OVTK_StimulationId_Number base offset (0x8100 = 33024)
    private const int OVTK_STIMULATION_OFFSET = 33024;
    
    /// <summary>
    /// Send event marker via LSL with numeric code
    /// Code is converted to OpenViBE format: OVTK_StimulationId_Number_XX
    /// </summary>
    public void SendMarker(int code, string description = "")
    {
        // Format for local logging: "S  1" (standard EEG marker format)
        string marker = $"S{code,3}";
        if (!string.IsNullOrEmpty(description))
        {
            marker = $"{marker}|{description}";
        }
        
        // Convert to OpenViBE OVTK format: code + 0x8100
        // Example: code 21 becomes 33045 = OVTK_StimulationId_Number_21
        int ovtkCode = OVTK_STIMULATION_OFFSET + code;
        
        // Send via LSL outlet (OpenViBE compatible int32)
        if (markerOutlet != null && isMarkerOutletReady)
        {
            int[] sample = new int[] { ovtkCode };
            markerOutlet.push_sample(sample);
            Debug.Log($"[LSLManager] Sent marker: {code} (OVTK: {ovtkCode})");
        }

        
        // Also log locally
        if (eegDataLogger != null)
        {
            eegDataLogger.LogEvent("LSL_MARKER", marker);
        }
    }
    
    /// <summary>
    /// Send event marker with text only (legacy support)
    /// </summary>
    public void SendMarkerText(string eventType, string eventData = "")
    {
        string marker = string.IsNullOrEmpty(eventData) ? eventType : $"{eventType}|{eventData}";
        
        // For backward compatibility: can't send text on int32 stream
        // Try to hash string to int or ignore?
        // For now, logging locally and ignoring LSL send if it's purely text
        
        if (markerOutlet != null && isMarkerOutletReady)
        {
            // Try to parse if it starts with number?
            // Otherwise, we can't send arbitrary string on int stream
            // Debug.LogWarning("[LSLManager] Cannot send text marker on int32 stream: " + marker);
        }

        
        if (eegDataLogger != null)
        {
            eegDataLogger.LogEvent("LSL_MARKER", marker);
        }
    }
    
    // ===== Marker Codes (Unified HRI EEG Convention) =====
    
    // Trial markers (1-9)
    public const int MARKER_TRIAL_START = 1;
    public const int MARKER_TRIAL_END = 9;
    
    // Participant action markers (10-19)
    public const int MARKER_ASSEMBLY_COMPLETE = 10;
    public const int MARKER_BUTTON_PRESS = 11;
    
    // Robot action markers - NORMAL (20-29)
    public const int MARKER_ROBOT_START = 20;
    public const int MARKER_ROBOT_PRESS_CYL = 21;
    public const int MARKER_ROBOT_PRESS_CUBE = 22;
    public const int MARKER_ROBOT_COMPLETE = 23;
    
    // Feedback markers (30-39)
    public const int MARKER_FEEDBACK_CORRECT = 30;
    public const int MARKER_FEEDBACK_WRONG = 31;
    public const int MARKER_FEEDBACK_ERROR = 32;  // False failure (showed wrong feedback)
    
    // Robot action markers - ERROR CONDITION (120-129)
    public const int MARKER_ROBOT_START_ERR = 120;
    public const int MARKER_ROBOT_PRESS_WRONG = 121;  // Wrong order press
    public const int MARKER_ROBOT_PRESS_MISS = 122;   // Spatial deviation press
    public const int MARKER_ROBOT_PRESS_AIR = 124;    // Air press / Shallow press
    public const int MARKER_ROBOT_PRESS_FREEZE = 125; // Hesitation
    public const int MARKER_ROBOT_PRESS_DOUBLE = 126; // Double tap
    public const int MARKER_ROBOT_PRESS_ABORT = 127;  // Abort / Omission
    public const int MARKER_ROBOT_COMPLETE_ERR = 123;
    
    // ===== Convenience methods =====
    public void SendTrialStart(int trialNum) => SendMarker(MARKER_TRIAL_START, $"T{trialNum}");
    public void SendTrialEnd(int trialNum) => SendMarker(MARKER_TRIAL_END, $"T{trialNum}");
    public void SendAssemblyComplete() => SendMarker(MARKER_ASSEMBLY_COMPLETE);
    public void SendButtonPress() => SendMarker(MARKER_BUTTON_PRESS);
    
    // Robot markers - call with isError flag
    public void SendRobotStart(bool isError = false) => 
        SendMarker(isError ? MARKER_ROBOT_START_ERR : MARKER_ROBOT_START);
    public void SendRobotPressCyl(bool isError = false) => 
        SendMarker(isError ? MARKER_ROBOT_PRESS_WRONG : MARKER_ROBOT_PRESS_CYL);
    public void SendRobotPressCube(bool isError = false) => 
        SendMarker(isError ? MARKER_ROBOT_PRESS_WRONG : MARKER_ROBOT_PRESS_CUBE);
    public void SendRobotComplete(bool isError = false) => 
        SendMarker(isError ? MARKER_ROBOT_COMPLETE_ERR : MARKER_ROBOT_COMPLETE);
    
    // Feedback markers
    public void SendFeedbackCorrect() => SendMarker(MARKER_FEEDBACK_CORRECT);
    public void SendFeedbackWrong() => SendMarker(MARKER_FEEDBACK_WRONG);
    public void SendFeedbackError() => SendMarker(MARKER_FEEDBACK_ERROR);
    
    void OnDestroy()
    {
        DisconnectEEGStream();
        
        if (markerOutlet != null)
        {
            markerOutlet = null;
        }
    }
    
    void OnApplicationQuit()
    {
        DisconnectEEGStream();
    }
}
