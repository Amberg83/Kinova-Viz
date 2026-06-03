using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class BackupUDPScript : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = 5005;

    [Header("Robot Links (Assign parent objects with ArticulationBodies)")]
    public ArticulationBody[] robotJoints = new ArticulationBody[6];

    private UdpClient udpClient;
    private Thread receiveThread;
    private float[] incomingPythonAngles = new float[6];
    private float[] activeTargets = new float[6];
    private float[] targetGoals = new float[6];
    private bool isRunning = true;
    private bool isInitialized = false;
    private bool hasNewData = false;
    private int packetCount = 0;
    private readonly object lockObject = new object();

    void Start()
    {
        Debug.Log("[UDP Receiver] System initializing...");

        receiveThread = new Thread(ReceiveData)
        {
            IsBackground = true
        };
        receiveThread.Start();

        // High stiffness and damping ensures the digital twin tightly mirrors the data
        foreach (ArticulationBody joint in robotJoints)
        {
            if (joint != null)
            {
                var drive = joint.xDrive;
                drive.stiffness = 10000f;
                drive.damping = 500f;
                drive.forceLimit = 1000f;
                joint.xDrive = drive;
            }
        }
    }

    private void ReceiveData()
    {
        try
        {
            udpClient = new UdpClient(port);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDP Receiver] Port error: {e.Message}");
            return;
        }

        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string csvString = Encoding.UTF8.GetString(data);

                // Split and parse the CSV string
                string[] tokens = csvString.Split(',');
                if (tokens != null && tokens.Length == 6)
                {
                    float[] receivedAngles = new float[6];
                    bool parseSuccess = true;

                    for (int i = 0; i < 6; i++)
                    {
                        // Use InvariantCulture to avoid errors with decimal point representation in different cultures (comma vs dot)
                        if (!float.TryParse(tokens[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out receivedAngles[i]))
                        {
                            parseSuccess = false;
                            break;
                        }
                    }

                    if (parseSuccess)
                    {
                        lock (lockObject)
                        {
                            Array.Copy(receivedAngles, incomingPythonAngles, 6);
                            hasNewData = true;
                        }

                        packetCount++;
                        // Log approximately once per second (every 20 packets at 20Hz stream rate) to prevent console spam
                        if (packetCount % 20 == 0)
                        {
                            Debug.Log($"[UDP Receiver] Telemetry incoming... Packet #{packetCount}. Joint Angles: [{string.Join(", ", receivedAngles)}]");
                        }
                    }
                }
            }
            catch (Exception) { /* Passive handling */ }
        }
    }

    void FixedUpdate()
    {
        float[] latestTelemetry = new float[6];
        bool gotNewPacket = false;

        lock (lockObject)
        {
            if (hasNewData)
            {
                Array.Copy(incomingPythonAngles, latestTelemetry, 6);
                hasNewData = false;
                gotNewPacket = true;
            }
        }

        // If we are not initialized yet, we must wait for the first real packet
        if (!isInitialized)
        {
            if (!gotNewPacket) return;

            for (int i = 0; i < 6; i++)
            {
                if (robotJoints[i] == null) continue;

                float targetAngle = latestTelemetry[i];
                while (targetAngle < -180f) targetAngle += 360f;
                while (targetAngle > 180f) targetAngle -= 360f;

                // First frame: Teleport the physical joint directly to the starting pose to avoid startup whip
                robotJoints[i].jointPosition = new ArticulationReducedSpace(targetAngle * Mathf.Deg2Rad);

                activeTargets[i] = targetAngle;
                targetGoals[i] = targetAngle;

                var drive = robotJoints[i].xDrive;
                drive.target = targetAngle;
                robotJoints[i].xDrive = drive;
            }

            isInitialized = true;
            Debug.Log("[UDP Receiver] Telemetry initialized and physical joints snapped smoothly with first packet.");
            return;
        }

        // 1. If we got a new packet, update the target goals with shortest-path unwrapping
        if (gotNewPacket)
        {
            for (int i = 0; i < 6; i++)
            {
                if (robotJoints[i] == null) continue;

                float targetAngle = latestTelemetry[i];
                while (targetAngle < -180f) targetAngle += 360f;
                while (targetAngle > 180f) targetAngle -= 360f;

                // Calculate the angular delta from our current active setpoint to the new target
                float delta = targetAngle - activeTargets[i];
                while (delta < -180f) delta += 360f;
                while (delta > 180f) delta -= 360f;

                // Update the goal in our continuous unwrapped setpoint space
                targetGoals[i] = activeTargets[i] + delta;
            }
        }

        // 2. On EVERY physics frame, smoothly interpolate our active targets towards the goals
        // We use a responsive Lerp with a time-corrected factor to ensure smooth, frame-rate independent glide
        float lerpFactor = 15f * Time.fixedDeltaTime; // 15f provides highly responsive yet exceptionally smooth tracking

        for (int i = 0; i < 6; i++)
        {
            if (robotJoints[i] == null) continue;

            // Glide smoothly towards the goal
            activeTargets[i] = Mathf.Lerp(activeTargets[i], targetGoals[i], lerpFactor);

            var drive = robotJoints[i].xDrive;
            drive.target = activeTargets[i];
            robotJoints[i].xDrive = drive;
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Interrupt();
    }
}
