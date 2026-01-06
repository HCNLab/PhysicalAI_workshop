// Assets/ControllerExtensions.cs
using UnityEngine;
using Unity.Robotics.UrdfImporter.Control; // Controller 타입을 쓰기 위해

public static class ControllerExtensions
{
    /// <summary>
    /// Extension method to "Controller" instance in Unity.Robotics.UrdfImporter.Control package
    /// q: target angle array, inRadians: whether the angles are in radians
    /// </summary>
    public static void MoveToTargets(this Controller ctrl, float[] q, bool inRadians = true, int offset = 1)
    {
        if (ctrl == null || q == null) return;
        Debug.Log("MoveToTargets called");
        // private field access is not possible, so we get the joints directly
        var joints = ctrl.GetComponentsInChildren<ArticulationBody>();
        if (joints == null || joints.Length <= offset) return;

        int n = Mathf.Min(q.Length, joints.Length - offset);
        for (int i = 0; i < n; i++)
        {
            var j = joints[i + offset];
            var d = j.xDrive;

            float deg = inRadians ? q[i] * Mathf.Rad2Deg : q[i];
            if (d.lowerLimit < d.upperLimit)
                deg = Mathf.Clamp(deg, d.lowerLimit, d.upperLimit);

            d.target = deg;
            j.xDrive = d;
        }
    }
}
