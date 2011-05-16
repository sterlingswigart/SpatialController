using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using xn;

namespace TrackingNI
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
        private const int SAMPLES_PER_SEC = 4;
        private const double SPHERE_RADIUS = 1.0;

        private bool calibrated;
        private UserGenerator userGenerator;
        private Device[] devices;

        //public object kinectDataLock;
        private object animationLock;
        private Ray3D[] raysToBeAnimated; // Note: Only works for one user.

        public Ray3D[] RaysToBeAnimated
        {
            get { lock (animationLock) { return raysToBeAnimated; } }
        }

        public SpatialController(ControllerStartup startupType, UserGenerator userGenerator, params Device[] devices)
        {
            this.raysToBeAnimated = new Ray3D[2]; // Left and right, in this case.
            this.userGenerator = userGenerator;
            //this.kinectDataLock = new object();
            this.animationLock = new object();

            int numRealDevices = 0; // TODO: List devices on the Z-wave network.
            if (numRealDevices == 0)
                this.calibrated = true; // We know where they are!
            else
                this.calibrated = false;

            this.devices = new Device[numRealDevices + devices.Length];
            for (int i = 0; i < devices.Length; i++)
            {
                this.devices[i] = devices[i];
            }

            Console.Write("Starting SpatialController!");

            switch (startupType)
            {
                case ControllerStartup.FromFile:
                    initFromFile();
                    calibrated = true;
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
        private void calibrate(uint user)
        {
            // TODO: Only calibrate real devices (since mock ones already have positions).
            int numDevices = 10/* TODO: number of devices */;
            Device[] devices = new Device[numDevices];

            Ray3D[] firstRays = new Ray3D[numDevices];
            // TODO: Prompt user to stand on right-back side of Kinect FOV with arms at sides.
            for (int i = 0; i < numDevices; i++)
                firstRays[i] = calibrateDeviceOnePosition(user);

            // TODO: Prompt user to stand on left-front side of Kinect FOV with arms at sides.
            for (int i = 0; i < numDevices; i++)
            {
                // TODO: If intersection with returns [0,0,0] vector, save this device #
                // and calibrate again on both sides.
                devices[i] = new Device(firstRays[i].intersectionWith(calibrateDeviceOnePosition(user)));
            }

            saveCalibrationToFile(devices);
            calibrated = true;  
            // TODO: Prompt user that calibration is complete.
        }

        private Ray3D calibrateDeviceOnePosition(uint user)
        {
            // TODO: Turn on device
            Thread.Sleep(CALIBRATION_OFFSET_SEC * 1000);
            Vector3D[] headPoints = new Vector3D[STEADY_SEC * SAMPLES_PER_SEC];
            Vector3D[] rightHandPoints = new Vector3D[STEADY_SEC * SAMPLES_PER_SEC];

            // Sample the user's hopefully steady hand.
            SkeletonJointPosition head = new SkeletonJointPosition();
            SkeletonJointPosition rightHand = new SkeletonJointPosition();
            for (int i = 0; i < STEADY_SEC * SAMPLES_PER_SEC; i++)
            {
                userGenerator.GetSkeletonCap().GetSkeletonJointPosition(user, SkeletonJoint.Head, ref head);
                userGenerator.GetSkeletonCap().GetSkeletonJointPosition(user, SkeletonJoint.RightHand, ref rightHand);
                headPoints[i] = new Vector3D(head.position.X, head.position.Y, head.position.Z);
                rightHandPoints[i] = new Vector3D(rightHand.position.X, rightHand.position.Y, rightHand.position.Z);
            }
            Thread.Sleep((CALIBRATION_SEC - STEADY_SEC - CALIBRATION_OFFSET_SEC) * 1000);

            // Take the averages of each side.
            Vector3D averageHeadPoint = new Vector3D(headPoints.Average(x => x.X),
                    headPoints.Average(x => x.Y), headPoints.Average(x => x.Z));
            Vector3D averageRightHandPoint = new Vector3D(rightHandPoints.Average(x => x.X),
                    rightHandPoints.Average(x => x.Y), rightHandPoints.Average(x => x.Z));

            // TODO: Turn off device.
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

            uint[] users = userGenerator.GetUsers();
            foreach (uint user in users)
            {
                if (userGenerator.GetSkeletonCap().IsTracking(user))
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

        private void checkUserGestures(uint id)
        {
            SkeletonJointPosition head = new SkeletonJointPosition();
            SkeletonJointPosition leftHand = new SkeletonJointPosition();
            SkeletonJointPosition rightHand = new SkeletonJointPosition();

            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.Head, ref head);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.LeftHand, ref leftHand);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.RightHand, ref rightHand);

            xn.Point3D headPoint = head.position;
            xn.Point3D leftPoint = leftHand.position;
            xn.Point3D rightPoint = rightHand.position;

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
