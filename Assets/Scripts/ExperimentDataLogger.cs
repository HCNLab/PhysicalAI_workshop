using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Experiment Data Logger
/// Records all experiment events and exports to CSV and JSON
/// </summary>
public class ExperimentDataLogger : MonoBehaviour
{
    [Header("Settings")]
    public string participantID = "P001";
    public string experimentCondition = "Default";
    public string outputFolder = "ExperimentData";

    [Header("Export Options")]
    public bool exportCSV = true;
    public bool exportJSON = true;

    [Header("References")]
    public HRIExperimentManager experimentManager;
    public AssemblyDetector assemblyDetector;

    // Data structures
    private List<TrialData> trialDataList = new List<TrialData>();
    private List<EventLog> eventLogs = new List<EventLog>();
    private TrialData currentTrial;

    [Serializable]
    public class TrialData
    {
        public int trialNumber;
        public string condition;
        public string errorType;
        public float startTime;
        public float endTime;
        public float totalDuration;
        public float assemblyDuration;
        public float robotReactionTime;
        public bool feedbackSuccess;
        public bool actualSuccess;
    }

    [Serializable]
    public class EventLog
    {
        public float timestamp;
        public int trialNumber;
        public string eventType;
        public string eventData;
    }

    [Serializable]
    public class SessionData
    {
        public string participantID;
        public string condition;
        public string startTime;
        public string endTime;
        public int totalTrials;
        public List<TrialData> trials;
        public List<EventLog> events;
    }

    void Start()
    {
        // Create output folder
        string fullPath = Path.Combine(Application.persistentDataPath, outputFolder);
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);

        // Subscribe to experiment events
        if (experimentManager != null)
        {
            experimentManager.OnTrialStart += LogTrialStart;
            experimentManager.OnTrialEnd += LogTrialEnd;
            experimentManager.OnRobotCommandPressed += LogRobotCommand;
        }

        if (assemblyDetector != null)
        {
            assemblyDetector.OnObjectPlaced += LogObjectPlaced;
            assemblyDetector.OnAssemblyComplete += LogAssemblyComplete;
        }

        Debug.Log($"[DataLogger] Initialized. Output: {fullPath}");
    }

    void LogTrialStart(int trialNum, float time)
    {
        currentTrial = new TrialData
        {
            trialNumber = trialNum,
            condition = experimentCondition,
            startTime = time
        };

        LogEvent("TRIAL_START", $"Trial {trialNum}");
    }

    void LogTrialEnd(int trialNum, float duration, bool success, HRIExperimentManager.ErrorType errorType)
    {
        if (currentTrial != null)
        {
            currentTrial.endTime = Time.time;
            currentTrial.totalDuration = duration;
            currentTrial.feedbackSuccess = success;
            currentTrial.errorType = errorType.ToString();
            
            // Actual success: only if no error type
            currentTrial.actualSuccess = (errorType == HRIExperimentManager.ErrorType.None);
            
            trialDataList.Add(currentTrial);
        }

        LogEvent("TRIAL_END", $"Duration: {duration:F2}s, Feedback: {(success ? "SUCCESS" : "FAILED")}, Error: {errorType}");
    }

    void LogRobotCommand(float reactionTime)
    {
        if (currentTrial != null)
            currentTrial.robotReactionTime = reactionTime;

        LogEvent("ROBOT_COMMAND", $"ReactionTime: {reactionTime:F2}s");
    }

    void LogObjectPlaced(string objectName)
    {
        LogEvent("OBJECT_PLACED", objectName);
    }

    void LogAssemblyComplete()
    {
        if (currentTrial != null)
            currentTrial.assemblyDuration = Time.time - currentTrial.startTime;

        LogEvent("ASSEMBLY_COMPLETE", "");
    }

    void LogEvent(string eventType, string data)
    {
        eventLogs.Add(new EventLog
        {
            timestamp = Time.time,
            trialNumber = currentTrial?.trialNumber ?? 0,
            eventType = eventType,
            eventData = data
        });

        Debug.Log($"[DataLogger] {eventType}: {data}");
    }

    /// <summary>
    /// Export all data to CSV and/or JSON files
    /// </summary>
    public void ExportData()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(Application.persistentDataPath, outputFolder);

        if (exportCSV)
        {
            // Export trial summary
            string trialFile = Path.Combine(folder, $"{participantID}_{timestamp}_trials.csv");
            ExportTrialData(trialFile);

            // Export event log
            string eventFile = Path.Combine(folder, $"{participantID}_{timestamp}_events.csv");
            ExportEventLog(eventFile);
        }

        if (exportJSON)
        {
            // Export all data as JSON
            string jsonFile = Path.Combine(folder, $"{participantID}_{timestamp}_session.json");
            ExportSessionJSON(jsonFile);
        }

        Debug.Log($"[DataLogger] Data exported to: {folder}");
    }

    void ExportSessionJSON(string filePath)
    {
        SessionData session = new SessionData
        {
            participantID = participantID,
            condition = experimentCondition,
            startTime = trialDataList.Count > 0 ? DateTime.Now.AddSeconds(-trialDataList[trialDataList.Count - 1].endTime).ToString("yyyy-MM-dd HH:mm:ss") : "",
            endTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            totalTrials = trialDataList.Count,
            trials = trialDataList,
            events = eventLogs
        };

        string json = JsonUtility.ToJson(session, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"[DataLogger] JSON exported: {filePath}");
    }

    void ExportTrialData(string filePath)
    {
        StringBuilder sb = new StringBuilder();
        
        // Header
        sb.AppendLine("ParticipantID,TrialNumber,Condition,ErrorType,TotalDuration,AssemblyDuration,RobotReactionTime,FeedbackSuccess,ActualSuccess");

        // Data
        foreach (var trial in trialDataList)
        {
            sb.AppendLine($"{participantID},{trial.trialNumber},{trial.condition},{trial.errorType},{trial.totalDuration:F3},{trial.assemblyDuration:F3},{trial.robotReactionTime:F3},{trial.feedbackSuccess},{trial.actualSuccess}");
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    void ExportEventLog(string filePath)
    {
        StringBuilder sb = new StringBuilder();

        // Header
        sb.AppendLine("ParticipantID,Timestamp,TrialNumber,EventType,EventData");

        // Data
        foreach (var evt in eventLogs)
        {
            sb.AppendLine($"{participantID},{evt.timestamp:F3},{evt.trialNumber},{evt.eventType},{evt.eventData}");
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    void OnApplicationQuit()
    {
        Debug.Log($"[DataLogger] OnApplicationQuit - trialDataList.Count={trialDataList.Count}, exportJSON={exportJSON}");
        // Auto-export on quit
        if (trialDataList.Count > 0)
            ExportData();
        else
            Debug.LogWarning("[DataLogger] No trial data to export!");
    }

    [ContextMenu("Export Data Now (CSV + JSON)")]
    void ExportFromMenu() => ExportData();

    [ContextMenu("Export JSON Only")]
    void ExportJSONOnly()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(Application.persistentDataPath, outputFolder);
        string jsonFile = Path.Combine(folder, $"{participantID}_{timestamp}_session.json");
        ExportSessionJSON(jsonFile);
    }

    [ContextMenu("Clear Data")]
    void ClearData()
    {
        trialDataList.Clear();
        eventLogs.Clear();
        Debug.Log("[DataLogger] Data cleared");
    }
}
