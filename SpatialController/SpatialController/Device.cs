using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using OpenZWaveDotNet;
using System.ComponentModel;

using OpenNI;

namespace SpatialController
{
    // Z-wave node
    public class Node
    {
        private Byte m_id = 0;
        public Byte ID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        private UInt32 m_homeId = 0;
        public UInt32 HomeID
        {
            get { return m_homeId; }
            set { m_homeId = value; }
        }

        private String m_name = "";
        public String Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        private String m_location = "";
        public String Location
        {
            get { return m_location; }
            set { m_location = value; }
        }

        private String m_label = "";
        public String Label
        {
            get { return m_label; }
            set { m_label = value; }
        }

        private String m_manufacturer = "";
        public String Manufacturer
        {
            get { return m_manufacturer; }
            set { m_manufacturer = value; }
        }

        private String m_product = "";
        public String Product
        {
            get { return m_product; }
            set { m_product = value; }
        }

        private List<ZWValueID> m_values = new List<ZWValueID>();
        public List<ZWValueID> Values
        {
            get { return m_values; }
        }

        public Node()
        {
        }

        public void AddValue(ZWValueID valueID)
        {
            m_values.Add(valueID);
        }

        public void RemoveValue(ZWValueID valueID)
        {
            m_values.Remove(valueID);
        }

        public void SetValue(ZWValueID valueID)
        {
            int valueIndex = -1;

            for (int index = 0; index < m_values.Count; index++)
            {
                if (m_values[index].GetId() == valueID.GetId())
                {
                    valueIndex = index;
                    break;
                }
            }

            if (valueIndex >= 0)
            {
                m_values[valueIndex] = valueID;
            }
            else
            {
                AddValue(valueID);
            }
        }
    }

    // Actual device class used by SpatialController
    public class Device
    {
        public const bool USE_ZWAVE = false;
        public const int NUM_MOCK_DEVICES = 1;

        // ==================================================
        // Z-wave interface code
        // ==================================================

        // Configuration details
        static private string logFilePath = @"C:\Users\Sterling\Desktop\My Dropbox\Classes\HomeNetworking\open-zwave-read-only\simplelog.txt";
        static private string zWaveConfigPath = @"C:\Users\Sterling\Desktop\My Dropbox\Classes\HomeNetworking\open-zwave-read-only\config\";
        static private string zWaveSerialPortName = @"\\.\COM25";

        static private ZWOptions m_options = null;
        static private ZWManager m_manager = null;
        static private Boolean m_nodesReady = false;
        static private UInt32 m_homeId = 0;
        static private BindingList<Node> m_nodeList = new BindingList<Node>();

        public static void SetUp()
        {
            if (USE_ZWAVE)
            {
                // Create the Options
                m_options = new ZWOptions();
                m_options.Create(zWaveConfigPath, @"", @"");

                // Lock the options
                m_options.Lock();

                // Create the OpenZWave Manager
                m_manager = new ZWManager();
                m_manager.Create();

                // Add an event handler for all the Z-Wave notifications
                m_manager.OnNotification += new ManagedNotificationsHandler(NotificationHandler);

                // Add a driver, this will start up the z-wave network
                m_manager.AddDriver(zWaveSerialPortName);

                // ZWave network is started, and our control of hardware can begin once all the nodes have reported in
                do
                {
                    //Log("Waiting", "The nodes all need to report in");
                    SleepForThreeSeconds(); // Wait since this process can take around 20seconds within my network
                }
                while (m_nodesReady == false);


                //Log("All nodes have reported in");
                foreach (Node node in m_nodeList)
                {
                    SleepForThreeSeconds(); // Not really needed, but allows you to see what is going on           
                    switch (m_manager.GetNodeType(m_homeId, node.ID).ToString())
                    {
                        case "Binary Power Switch":
                        case "Multilevel Power Switch":
                        case "Multilevel Switch":
                            //Log("Turning On " + m_manager.GetNodeType(m_homeId, node.ID).ToString() + " with ID " + node.ID.ToString());
                            //m_manager.SetNodeOn(m_homeId, node.ID);
                            break;
                        default:
                            //Log("Can't do anything with " + m_manager.GetNodeType(m_homeId, node.ID).ToString());
                            break;
                    }
                }
                SleepForThreeSeconds();
                //Log("All switches should now be on");
                //Log("Press a key to exit");
                //String commandRead = Console.ReadLine().ToString();
            }
        }

        public static byte[] getNodes()
        {
            if (USE_ZWAVE)
            {
                byte[] nodes = new byte[m_nodeList.Count];
                for (int i = 0; i < m_nodeList.Count; i++)
                    nodes[i] = m_nodeList[i].ID;
                return nodes;
            }
            else
            {
                byte[] nodes = new byte[NUM_MOCK_DEVICES];
                for (int i = 0; i < NUM_MOCK_DEVICES; i++)
                    nodes[i] = (byte)i;
                return nodes;
            }
        }

        public static void turnOn(byte deviceId)
        {
            if (USE_ZWAVE)
                m_manager.SetNodeOn(m_homeId, deviceId);
        }

        public static void turnOff(byte deviceId)
        {
            if (USE_ZWAVE)
                m_manager.SetNodeOff(m_homeId, deviceId);
        }

        /// <summary>
        /// Simple logging method to keep track of actions
        /// </summary>
        /// <param name="type">Definition of the log enry e.g. Message, Error</param>
        /// <param name="message">The message to log</param>
        static private void Log(string type, string message)
        {
            string entry = String.Format("{0}\t{1}\t{2}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), type, message);
            if (logFilePath.Length > 0)
            {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(logFilePath, true))
                {
                    sw.WriteLine(entry);
                }
            }
            Console.Write(entry);

        }

        static private void Log(string message)
        {
            Log("Message", message);
        }

        /// <summary>
        /// Wait for 3 seconds while other work goes on
        /// </summary>
        static private void SleepForThreeSeconds()
        {
            System.Threading.Thread.Sleep(3000);
        }

        /// <summary>
        /// Method which handles the events raised by the ZWave network
        /// </summary>
        /// <param name="m_notification"></param>
        static private void NotificationHandler(ZWNotification m_notification)
        {
            if (m_notification == null) { return; }

            switch (m_notification.GetType())
            {
                case ZWNotification.Type.ValueAdded:
                    {
                        Node node = GetNode(m_notification.GetHomeId(), m_notification.GetNodeId());
                        if (node != null)
                        {
                            node.AddValue(m_notification.GetValueID());
                            //Log("Event", "Node ValueAdded, " + m_manager.GetValueLabel(m_notification.GetValueID()));


                        }
                        break;
                    }

                case ZWNotification.Type.ValueRemoved:
                    {
                        Node node = GetNode(m_notification.GetHomeId(), m_notification.GetNodeId());
                        if (node != null)
                        {
                            node.RemoveValue(m_notification.GetValueID());
                            //Log("Event", "Node ValueRemoved, " + m_manager.GetValueLabel(m_notification.GetValueID()));
                        }
                        break;
                    }

                case ZWNotification.Type.ValueChanged:
                    {
                        Node node = GetNode(m_notification.GetHomeId(), m_notification.GetNodeId());
                        if (node != null)
                        {
                            node.SetValue(m_notification.GetValueID());

                            //Log("Event", "Node ValueChanged, " + m_manager.GetValueLabel(m_notification.GetValueID()).ToString());
                        }

                        break;
                    }

                case ZWNotification.Type.Group:
                    {
                        break;
                    }

                case ZWNotification.Type.NodeAdded:
                    {
                        // Add the new node to our list
                        Node node = new Node();
                        node.ID = m_notification.GetNodeId();
                        node.HomeID = m_notification.GetHomeId();
                        m_nodeList.Add(node);
                        //Log("Event", "Node added, " + m_notification.GetNodeId().ToString());
                        break;
                    }

                case ZWNotification.Type.NodeRemoved:
                    {
                        foreach (Node node in m_nodeList)
                        {
                            if (node.ID == m_notification.GetNodeId())
                            {
                                //Log("Event", "Node removed, " + m_notification.GetNodeId().ToString());
                                m_nodeList.Remove(node);
                                break;
                            }
                        }
                        break;
                    }

                case ZWNotification.Type.NodeProtocolInfo:
                    {
                        Node node = GetNode(m_notification.GetHomeId(), m_notification.GetNodeId());
                        if (node != null)
                        {

                            node.Label = m_manager.GetNodeType(m_homeId, node.ID);
                            //Log("Event", "Node protocol info, " + node.Label.ToString());
                        }
                        break;
                    }

                case ZWNotification.Type.NodeNaming:
                    {
                        Node node = GetNode(m_notification.GetHomeId(), m_notification.GetNodeId());
                        if (node != null)
                        {
                            node.Manufacturer = m_manager.GetNodeManufacturerName(m_homeId, node.ID);
                            node.Product = m_manager.GetNodeProductName(m_homeId, node.ID);
                            node.Location = m_manager.GetNodeLocation(m_homeId, node.ID);
                            node.Name = m_manager.GetNodeName(m_homeId, node.ID);
                        }
                        break;
                    }

                case ZWNotification.Type.NodeEvent:
                    {
                        //Log("Event", "Node event");
                        break;
                    }

                case ZWNotification.Type.PollingDisabled:
                    {
                        //Log("Event", "Polling disabled notification");
                        break;
                    }

                case ZWNotification.Type.PollingEnabled:
                    {
                        //Log("Event", "Polling disabled notification");
                        break;
                    }

                case ZWNotification.Type.DriverReady:
                    {
                        m_homeId = m_notification.GetHomeId();
                        //Log("Event", "Driver ready, with homeId " + m_homeId.ToString());
                        break;
                    }
                case ZWNotification.Type.NodeQueriesComplete:
                    {
                        break;
                    }
                case ZWNotification.Type.AllNodesQueried:
                    {
                        //Log("Event", "All nodes queried");
                        m_nodesReady = true;
                        break;
                    }
                case ZWNotification.Type.AwakeNodesQueried:
                    {
                        //Log("Event", "Awake nodes queried (but some sleeping nodes have not been queried)");
                        break;
                    }
            }

        }

        /// <summary>
        /// Gets a node based on the homeId and the nodeId
        /// </summary>
        /// <param name="homeId"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        static private Node GetNode(UInt32 homeId, Byte nodeId)
        {
            foreach (Node node in m_nodeList)
            {
                if ((node.ID == nodeId) && (node.HomeID == homeId))
                {
                    return node;
                }
            }

            return null;
        }

        static private ZWValueID GetValueID(Node node, string valueLabel)
        {
            foreach (ZWValueID valueID in node.Values)
            {
                if (m_manager.GetValueLabel(valueID) == valueLabel)
                {
                    return valueID;
                }
            }
            return null;
        }

        // ==================================================
        // Focusing code
        // ==================================================

        private const int ACTIVATION_MS = 400;
        private const int DEBOUNCE_MS = 2000;

        private bool inFocus;
        private bool on;

        private DateTime lastActionTime;
        private DateTime focusStartTime;

        public Vector3D position;

        private byte deviceId;

        public Device(Vector3D position, byte deviceId)
        {
            this.on = false;
            this.inFocus = false;
            this.lastActionTime = DateTime.Now;
            this.focusStartTime = DateTime.Now;
            this.position = position;
            this.deviceId = deviceId;
        }

        public void isInFocus()
        {
            if (inFocus)
            {
                Console.Write("In focus!");
                if (DateTime.Now - focusStartTime > new TimeSpan(0, 0, 0, 0, ACTIVATION_MS)
                    && DateTime.Now - lastActionTime > new TimeSpan(0, 0, 0, 0, DEBOUNCE_MS))
                {
                    if (on)
                        Console.Write("Switching light off!");
                    else
                        Console.Write("Switching light on!");
                    if (USE_ZWAVE)
                    {
                        if (!on)
                            m_manager.SetNodeOn(m_homeId, deviceId);
                        else
                            m_manager.SetNodeOff(m_homeId, deviceId);
                    }
                    on = !on;
                    lastActionTime = DateTime.Now;
                }
            }
            else
            {
                Console.Write("Not in focus!");
                inFocus = true;
                focusStartTime = DateTime.Now;
            }
        }

        public void isNotInFocus()
        {
            if (inFocus)
            {
                // Reset state.
                inFocus = false;
            }
        }
    }
}
