using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using OpenNI;

namespace SpatialController
{
    enum ControllerStartup
    {
        FromFile,
        Calibrate
    }

    class SpatialController
    {
        public const string CALIBRATION_DATA_FILE = "calibration.txt";
        private const int CALIBRATION_SEC = 9;
        private const int CALIBRATION_OFFSET_SEC = 4;
        private const int STEADY_SEC = 3;
        private const int SEC_FOR_RELOCATION = 5;
        private const int SEC_BETWEEN_CALIBRATIONS = 2;
        private const int SAMPLES_PER_SEC = 4;
        private const double SPHERE_RADIUS = 1.0;

        private bool calibrated;
        private UserGenerator userGenerator;
        private Device[] devices;

        private object animationLock;
        private Ray3D[] raysToBeAnimated; // Note: Only works for one user.

        public Ray3D[] RaysToBeAnimated
        {
            get { lock (animationLock) { return raysToBeAnimated; } }
        }

        public SpatialController(ControllerStartup startupType, UserGenerator userGenerator)
        {
            this.raysToBeAnimated = new Ray3D[2]; // Left and right, in this case.
            this.userGenerator = userGenerator;
            this.animationLock = new object();
            this.calibrated = false;

            this.devices = new Device[Device.getNodes().Count];
            for (int i = 0; i < devices.Length; i++)
            {
                this.devices[i] = devices[i];
            }

            Console.Write("Starting SpatialController!");

            switch (startupType)
            {
                case ControllerStartup.FromFile:
                    initFromFile();
                    this.calibrated = true;
                    break;
                case ControllerStartup.Calibrate:
                    // Wait for user to be recognized to start calibration.
                    break;
                default:
                    break;
            }
        }

        // Calibrate the locations of devices in the room, saving calibration
        // data in CALIBRATION_DATA_FILE. Use only right hand for pointing.
        private void calibrate(int user)
        {
            List<byte> nodes = Device.getNodes();
            Ray3D[] firstRays = new Ray3D[nodes.Count];
            UserPrompt.Write("Please stand about 10 feet away from the kinect on the right"
                    + " side of the field of view, but leave room for pointing off to the right.");
            UserPrompt.Write("When a light turns on, please point to it until it turns off.");
            Thread.Sleep((SEC_FOR_RELOCATION - SEC_BETWEEN_CALIBRATIONS) * 1000);

            for (int i = 0; i < nodes.Count; i++)
            {
                Thread.Sleep(SEC_BETWEEN_CALIBRATIONS * 1000);
                firstRays[i] = calibrateDeviceOnePosition(user, nodes[i]);
            }

            UserPrompt.Write("Please stand about 5 feet away from the kinect on the left"
                    + " side of the field of view, but leave room for pointing off to the left.");
            UserPrompt.Write("Once again, when a light turns on, please point to it until it turns off.");
            Thread.Sleep((SEC_FOR_RELOCATION - SEC_BETWEEN_CALIBRATIONS) * 1000);

            for (int i = 0; i < nodes.Count; i++)
            {
                Thread.Sleep(SEC_BETWEEN_CALIBRATIONS * 1000);
                devices[i] = new Device(firstRays[i].intersectionWith(calibrateDeviceOnePosition(user, nodes[i])), nodes[i]);
            }

            saveCalibrationToFile(devices);
            calibrated = true;
            UserPrompt.Write("Calibration has been completed. After a few seconds, you should be able"
                    + " to point to lights to turn them on!");
        }

        private Ray3D calibrateDeviceOnePosition(int user, byte device)
        {
            UserPrompt.Write("Turning on device " + device);
            Device.turnOn(device);
            Thread.Sleep(CALIBRATION_OFFSET_SEC * 1000);
            Vector3D[] headPoints = new Vector3D[STEADY_SEC * SAMPLES_PER_SEC];
            Vector3D[] rightHandPoints = new Vector3D[STEADY_SEC * SAMPLES_PER_SEC];

            // Sample the user's hopefully steady hand.
            SkeletonJointPosition head = new SkeletonJointPosition();
            SkeletonJointPosition rightHand = new SkeletonJointPosition();
            for (int i = 0; i < STEADY_SEC * SAMPLES_PER_SEC; i++)
            {
                head = userGenerator.SkeletonCapability.GetSkeletonJointPosition(user, SkeletonJoint.Head);
                rightHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(user, SkeletonJoint.RightHand);
                headPoints[i] = new Vector3D(head.Position.X, head.Position.Y, head.Position.Z);
                rightHandPoints[i] = new Vector3D(rightHand.Position.X, rightHand.Position.Y, rightHand.Position.Z);
            }
            Thread.Sleep((CALIBRATION_SEC - STEADY_SEC - CALIBRATION_OFFSET_SEC) * 1000);

            // Take the averages of each side.
            Vector3D averageHeadPoint = new Vector3D(headPoints.Average(x => x.X),
                    headPoints.Average(x => x.Y), headPoints.Average(x => x.Z));
            Vector3D averageRightHandPoint = new Vector3D(rightHandPoints.Average(x => x.X),
                    rightHandPoints.Average(x => x.Y), rightHandPoints.Average(x => x.Z));

            UserPrompt.Write("Turning off device " + device);
            Device.turnOff(device);
            return new Ray3D(averageHeadPoint, averageRightHandPoint);
        }

        // Saves device calibration data so that the system remembers the positions.
        private void saveCalibrationToFile(Device[] devices)
        {
            // TODO

            // Template:
            // FriendlyName[\t]x[\t]y[\t]z
            // ...
        }

        // Initialize the locations of devices from a file.
        private void initFromFile()
        {
            // TODO

            // Template:
            // FriendlyName[\t]x[\t]y[\t]z
            // ...
        }

        // Should be called intermittently to examine skeleton data. Determines
        // whether any gestures have been completed and interacts with the gestures'
        // targets.
        public void checkGestures()
        {
            Console.Write("Called checkGestures().");

            int[] users = userGenerator.GetUsers();
            foreach (int user in users)
            {
                if (userGenerator.SkeletonCapability.IsTracking(user))
                {
                    if (!calibrated)
                    {
                        // Use the first user to calibrate system.
                        calibrate(user);
                        break;
                    }
                    checkUserGestures(user);
                }
                else
                {
                    for (int i = 0; i < raysToBeAnimated.Length; i++)
                        raysToBeAnimated[i] = null;
                }
            }
        }

        private void checkUserGestures(int id)
        {
            SkeletonJointPosition head = new SkeletonJointPosition();
            SkeletonJointPosition leftHand = new SkeletonJointPosition();
            SkeletonJointPosition rightHand = new SkeletonJointPosition();

            head = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.Head);
            leftHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.LeftHand);
            rightHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.RightHand);

            OpenNI.Point3D headPoint = head.Position;
            OpenNI.Point3D leftPoint = leftHand.Position;
            OpenNI.Point3D rightPoint = rightHand.Position;

            Ray3D leftPointer = new Ray3D(headPoint.X, headPoint.Y, headPoint.Z,
                    leftPoint.X, leftPoint.Y, leftPoint.Z);
            Ray3D rightPointer = new Ray3D(headPoint.X, headPoint.Y, headPoint.Z,
                    rightPoint.X, rightPoint.Y, rightPoint.Z);

            lock (animationLock)
            {
                raysToBeAnimated[0] = leftPointer;
                raysToBeAnimated[1] = rightPointer;
            }

            Console.Write("Left vector: " + leftPointer);
            Console.Write("Right vector: " + rightPointer);

            foreach (Device d in devices)
            {
                if (leftPointer.closeTo(d.position) || rightPointer.closeTo(d.position))
                {
                    d.isInFocus();
                    // The Device class does the actual device manipulation.
                    // In the future, the calibration step will be able to change what each
                    // device's action is.
                }
            }
            Console.Write("=============================");
        }
    }
}
