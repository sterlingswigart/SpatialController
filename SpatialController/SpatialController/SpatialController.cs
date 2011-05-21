using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SpeechLib;
using System.Speech;
using System.Speech.Synthesis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

using OpenNI;

namespace SpatialController
{
    enum ControllerStartup
    {
        FromFile,
        Calibrate
    }

    public class Sound
    {
        private byte[] m_soundBytes;
        private string m_fileName;

        private enum Flags
        {
            SND_SYNC = 0x0000,  /* play synchronously (default) */
            SND_ASYNC = 0x0001,  /* play asynchronously */
            SND_NODEFAULT = 0x0002,  /* silence (!default) if sound not found */
            SND_MEMORY = 0x0004,  /* pszSound points to a memory file */
            SND_LOOP = 0x0008,  /* loop the sound until next sndPlaySound */
            SND_NOSTOP = 0x0010,  /* don't stop any currently playing sound */
            SND_NOWAIT = 0x00002000, /* don't wait if the driver is busy */
            SND_ALIAS = 0x00010000, /* name is a registry alias */
            SND_ALIAS_ID = 0x00110000, /* alias is a predefined ID */
            SND_FILENAME = 0x00020000, /* name is file name */
            SND_RESOURCE = 0x00040004  /* name is resource name or atom */
        }

        [DllImport("CoreDll.DLL", EntryPoint = "PlaySound", SetLastError = true)]
        private extern static int WCE_PlaySound(string szSound, IntPtr hMod, int flags);

        [DllImport("CoreDll.DLL", EntryPoint = "PlaySound", SetLastError = true)]
        private extern static int WCE_PlaySoundBytes(byte[] szSound, IntPtr hMod, int flags);

        /// <summary>
        /// Construct the Sound object to play sound data from the specified file.
        /// </summary>
        public Sound(string fileName)
        {
            m_fileName = fileName;
        }

        /// <summary>
        /// Construct the Sound object to play sound data from the specified stream.
        /// </summary>
        public Sound(Stream stream)
        {
            // read the data from the stream
            m_soundBytes = new byte[stream.Length];
            stream.Read(m_soundBytes, 0, (int)stream.Length);
        }

        /// <summary>
        /// Play the sound
        /// </summary>
        public void Play()
        {
            // if a file name has been registered, call WCE_PlaySound,
            //  otherwise call WCE_PlaySoundBytes
            if (m_fileName != null)
                WCE_PlaySound(m_fileName, IntPtr.Zero, (int)(Flags.SND_ASYNC | Flags.SND_FILENAME));
            else
                WCE_PlaySoundBytes(m_soundBytes, IntPtr.Zero, (int)(Flags.SND_ASYNC | Flags.SND_MEMORY));
        }
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

        private SpeechSynthesizer synth;

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

            this.devices = new Device[Device.getNodes().Length];
            for (int i = 0; i < devices.Length; i++)
            {
                this.devices[i] = devices[i];
            }

            synth = new SpeechSynthesizer();
            synth.SelectVoice("Microsoft Anna");
            //To be removed later
            synth.Speak("Starting SpatialController!");

            //Play the beep sound to test it
            Sound sound = new Sound ("\\beep-6.wav");
            sound.Play();
            

            //Console.Write("Starting SpatialController!");

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
            byte[] nodes = Device.getNodes();
            Ray3D[] firstRays = new Ray3D[devices.Length];

            synth.Speak("Please stand about 10 feet away from the kinect on the right"
                    + " side of the field of view, but leave room for pointing off to the right.");

            //UserPrompt.Write("Please stand about 10 feet away from the kinect on the right"
            //        + " side of the field of view, but leave room for pointing off to the right.");

            synth.Speak("When a light turns on, please point to it until it turns off.");

            //UserPrompt.Write("When a light turns on, please point to it until it turns off.");
            Thread.Sleep((SEC_FOR_RELOCATION - SEC_BETWEEN_CALIBRATIONS) * 1000);

            for (int i = 0; i < devices.Length; i++)
            {
                Thread.Sleep(SEC_BETWEEN_CALIBRATIONS * 1000);
                firstRays[i] = calibrateDeviceOnePosition(user, nodes[i]);
            }

            synth.Speak("Once again, when a light turns on, please point to it until it turns off.");

            //UserPrompt.Write("Please stand about 5 feet away from the kinect on the left"
            //        + " side of the field of view, but leave room for pointing off to the left.");
            //UserPrompt.Write("Once again, when a light turns on, please point to it until it turns off.");
            Thread.Sleep((SEC_FOR_RELOCATION - SEC_BETWEEN_CALIBRATIONS) * 1000);

            for (int i = 0; i < devices.Length; i++)
            {
                Thread.Sleep(SEC_BETWEEN_CALIBRATIONS * 1000);
                devices[i] = new Device(firstRays[i].intersectionWith(calibrateDeviceOnePosition(user, nodes[i])), nodes[i]);
            }

            saveCalibrationToFile(devices);
            calibrated = true;

            synth.Speak("Calibration has been completed. After a few seconds, you should be able"
                    + " to point to lights to turn them on!");
            //UserPrompt.Write("Calibration has been completed. After a few seconds, you should be able"
            //        + " to point to lights to turn them on!");
        }

        private Ray3D calibrateDeviceOnePosition(int user, byte device)
        {

            synth.Speak("Turning on device " + device);
            //UserPrompt.Write("Turning on device " + device);
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

            synth.Speak("Turning off device " + device);
            //UserPrompt.Write("Turning off device " + device);
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
