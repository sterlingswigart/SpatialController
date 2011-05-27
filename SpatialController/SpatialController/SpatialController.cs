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

    delegate void RecalibrateEventHandler(object sender, EventArgs e);

    class SpatialController
    {
        public event RecalibrateEventHandler RecalibrateCommand;

        public const string CALIBRATION_DATA_FILE = "calibration.txt";
        private const int CALIBRATION_SEC = 9;
        private const int CALIBRATION_OFFSET_SEC = 4;
        private const int STEADY_SEC = 3;
        private const int SEC_FOR_RELOCATION = 5;
        private const int SEC_BETWEEN_CALIBRATIONS = 2;
        private const int SAMPLES_PER_SEC = 4;

        private const double MAX_DIMMING_HAND_OFFSET_Y = 100.0;
        private const double TOTAL_DIMMING_DISTANCE = 600.0;

        private bool calibrated;
        private UserGenerator userGenerator;
        private Device[] devices;

        private object animationLock;
        private Ray3D[] raysToBeAnimated; // Note: Only works for one user.

        private SpeechSynthesizer synth;
        private SpeechLib.SpSharedRecoContext objRecoContext = null;
        private SpeechLib.ISpeechRecoGrammar grammar = null;
        private SpeechLib.ISpeechGrammarRule menuRule = null;

        private bool dimmingDown;
        private bool dimmingUp;
        private double dimmingStartY;

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

            synth = new SpeechSynthesizer();
            synth.SelectVoice("Microsoft Anna");

            SpeakAndWriteToPrompt("Starting SpatialController!");

            switch (startupType)
            {
                case ControllerStartup.FromFile:
                    this.calibrated = true;
                    initFromFile();
                    break;
                case ControllerStartup.Calibrate:
                    // Wait for user to be recognized to start calibration.
                    break;
                default:
                    break;
            }

            dimmingDown = false;
            dimmingUp = false;
            dimmingStartY = -1000.0;

            VoiceCalibration();
        }

        // Calibrate the locations of devices in the room, saving calibration
        // data in CALIBRATION_DATA_FILE. Use only right hand for pointing.
        private void calibrate(int user)
        {
            List<byte> nodes = Device.getNodes();
            Ray3D[] firstRays = new Ray3D[nodes.Count];

            SpeakAndWriteToPrompt("Please stand about 10 feet away from the kinect on the right"
                    + " side of the field of view, but leave room for pointing off to the right.");

            SpeakAndWriteToPrompt("When a light turns on, please point to it until it turns off.");

            Thread.Sleep((SEC_FOR_RELOCATION - SEC_BETWEEN_CALIBRATIONS) * 1000);

            for (int i = 0; i < nodes.Count; i++)
            {
                Thread.Sleep(SEC_BETWEEN_CALIBRATIONS * 1000);
                firstRays[i] = calibrateDeviceOnePosition(user, nodes[i]);
            }

            SpeakAndWriteToPrompt("Please stand about 5 feet away from the kinect on the left"
                    + " side of the field of view, but leave room for pointing off to the left.");

            SpeakAndWriteToPrompt("Once again, when a light turns on, please point to it until it turns off.");

            Thread.Sleep((SEC_FOR_RELOCATION - SEC_BETWEEN_CALIBRATIONS) * 1000);

            for (int i = 0; i < nodes.Count; i++)
            {
                Thread.Sleep(SEC_BETWEEN_CALIBRATIONS * 1000);
                devices[i] = new Device(firstRays[i].intersectionWith(calibrateDeviceOnePosition(user, nodes[i])), nodes[i]);
            }

            saveCalibrationToFile(devices);
            calibrated = true;

            SpeakAndWriteToPrompt("Calibration has been completed. After a few seconds, you should be able"
                    + " to point to lights to turn them on!");
        }

        private void SpeakAndWriteToPrompt(String s)
        {
            UserPrompt.Write(s);
            synth.Speak(s);
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
            TextWriter tw = new StreamWriter(CALIBRATION_DATA_FILE);

            for (int i = 0; i < devices.Length; i++)
            {
                //each device uses four lines (or more depending on what information
                //we need to save later)
                tw.WriteLine(devices[i].deviceId);
                tw.WriteLine(devices[i].position.X);
                tw.WriteLine(devices[i].position.Y);
                tw.WriteLine(devices[i].position.Z);
            }

            tw.Close();
        }

        // Initialize the locations of devices from a file.
        private void initFromFile()
        {
            TextReader tr = new StreamReader(CALIBRATION_DATA_FILE);
            List<byte> nodes = Device.getNodes();

            int i = 0;
            //we need to check that peek returns "null" in case there is no other line to read
            while ( tr.Peek() > -1 )
            {
                byte deviceId = Convert.ToByte(tr.ReadLine());
                if (!nodes.Contains(deviceId))
                {
                    this.calibrated = false;
                    return;
                }

                //we need to double check the convert to byte function since this has not been tested
                devices[i].deviceId = deviceId;
                devices[i].position.X = Convert.ToDouble(tr.ReadLine());
                devices[i].position.Y = Convert.ToDouble(tr.ReadLine());
                devices[i].position.Z = Convert.ToDouble(tr.ReadLine());
                i++;
            }

            tr.Close();
            
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
            SkeletonJointPosition leftShoulder = new SkeletonJointPosition();
            SkeletonJointPosition rightShoulder = new SkeletonJointPosition();

            head = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.Head);
            leftHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.LeftHand);
            rightHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.RightHand);
            leftShoulder = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.LeftShoulder);
            rightShoulder = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.RightShoulder);

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
            
            if (VerticallyClose(leftPoint, rightPoint))
            {
                // Handle dimming.
                if (FirstAboveSecond(leftPoint, headPoint) && FirstAboveSecond(rightPoint, headPoint))
                {
                    Console.Write("Beginning dim down!");
                    dimmingDown = true;
                    dimmingStartY = leftPoint.Y;
                }
                else if (dimmingDown)
                {
                    int dimPercent = (int)((dimmingStartY - leftPoint.Y) / TOTAL_DIMMING_DISTANCE);
                    if (dimPercent < 0) dimPercent = 0;
                    else if (dimPercent > 100) dimPercent = 100;

                    Device.dimAllToPercent(dimPercent);
                }
                else if (VerticallyClose(leftPoint, leftShoulder.Position)
                    && VerticallyClose(leftShoulder.Position, rightShoulder.Position)
                    && VerticallyClose(rightShoulder.Position, rightPoint))
                {
                    Console.Write("Beginning dim up!");
                    dimmingUp = true;
                    dimmingStartY = leftPoint.Y;
                }
                else if (dimmingUp)
                {
                    int dimPercent = (int)((leftPoint.Y - dimmingStartY) / TOTAL_DIMMING_DISTANCE);
                    if (dimPercent < 0) dimPercent = 0;
                    else if (dimPercent > 100) dimPercent = 100;

                    Device.dimAllToPercent(dimPercent);
                }
            }
            else
            {
                // Allow pointing.
                dimmingDown = false;
                dimmingUp = false;
            }

            if (!dimmingUp && !dimmingDown) foreach (Device d in devices)
            {
                if (leftPointer.closeTo(d.position) || rightPointer.closeTo(d.position))
                {
                    d.isInFocus();
                }
            }
            Console.Write("=============================");
        }

        private bool VerticallyClose(OpenNI.Point3D p0, OpenNI.Point3D p1)
        {
            return Math.Abs(p0.Y - p1.Y) < MAX_DIMMING_HAND_OFFSET_Y;
        }

        private bool FirstAboveSecond(OpenNI.Point3D p0, OpenNI.Point3D p1)
        {
            return p0.Y - p1.Y > 0;
        }

        private void Reco_Event(int StreamNumber, object StreamPosition, SpeechRecognitionType RecognitionType, ISpeechRecoResult Result)
        {
            String text = Result.PhraseInfo.GetText(0, -1, true);
            synth.Speak("Recognition: " + text); // DEBUG

            // TODO: For "Do you mean?" functionality, check yes/no NOT in the following function
            //      because they are not actions.

            DoActionFromVoiceCommand(text);
        }

        private void Hypo_Event(int StreamNumber, object StreamPosition, ISpeechRecoResult Result)
        {
            String text = Result.PhraseInfo.GetText(0, -1, true);
            synth.Speak("Hypothesis: " + text); // DEBUG
            
            // TODO: Did you mean? If "yes", call DoActionFromVoiceCommand(text).
        }

        private void DoActionFromVoiceCommand(string command)
        {
            command = command.ToLower();
            if (command.Contains("light "))
            {
                int lightIndex = -1;
                if (command.Contains("first"))
                    lightIndex = 1;
                else if (command.Contains("second"))
                    lightIndex = 2;
                else if (command.Contains("third"))
                    lightIndex = 3;

                if (lightIndex > 0 && devices.Length < lightIndex)
                {
                    if (command.Contains("on"))
                        Device.turnOn(devices[lightIndex].deviceId);
                    else if (command.Contains("off"))
                        Device.turnOff(devices[lightIndex].deviceId);
                }
            }
            else if (command.Contains("all lights"))
            {
                if (command.Contains("on"))
                    Device.turnOnAll();
                else if (command.Contains("off"))
                    Device.turnOffAll();
            }
            else if (command.Contains("recalibrate"))
            {
                if (RecalibrateCommand != null)
                    RecalibrateCommand(this, EventArgs.Empty);
            }
        }

        private void VoiceCalibration()
        {
            // Get an insance of RecoContext. I am using the shared RecoContext.
            objRecoContext = new SpeechLib.SpSharedRecoContext();
            // Assign a eventhandler for the Hypothesis Event.
            objRecoContext.Hypothesis += new _ISpeechRecoContextEvents_HypothesisEventHandler(Hypo_Event);
            // Assign a eventhandler for the Recognition Event.
            objRecoContext.Recognition += new _ISpeechRecoContextEvents_RecognitionEventHandler(Reco_Event);
            //Creating an instance of the grammer object.
            grammar = objRecoContext.CreateGrammar(0);

            //Activate the Menu Commands.
            menuRule = grammar.Rules.Add("MenuCommands", SpeechRuleAttributes.SRATopLevel | SpeechRuleAttributes.SRADynamic, 1);
            object PropValue = "";
            menuRule.InitialState.AddWordTransition(null, "Cancel", " ", SpeechGrammarWordType.SGLexical, "Cancel", 1, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "Yes", " ", SpeechGrammarWordType.SGLexical, "Yes", 2, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "No", " ", SpeechGrammarWordType.SGLexical, "No", 3, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "All Lights Off", " ", SpeechGrammarWordType.SGLexical, "All Lights Off", 4, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "All Lights On", " ", SpeechGrammarWordType.SGLexical, "All Lights On", 5, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "First Light On", " ", SpeechGrammarWordType.SGLexical, "First Light On", 6, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "Second Light On", " ", SpeechGrammarWordType.SGLexical, "Second Light On", 7, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "Third Light On", " ", SpeechGrammarWordType.SGLexical, "Third Light On", 8, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "First Light Off", " ", SpeechGrammarWordType.SGLexical, "First Light Off", 9, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "Second Light Off", " ", SpeechGrammarWordType.SGLexical, "Second Light Off", 10, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "Third Light Off", " ", SpeechGrammarWordType.SGLexical, "Third Light Off", 11, ref PropValue, 1.0F);
            menuRule.InitialState.AddWordTransition(null, "Recalibrate", " ", SpeechGrammarWordType.SGLexical, "Recalibrate", 12, ref PropValue, 1.0F);
            grammar.Rules.Commit();
            grammar.CmdSetRuleState("MenuCommands", SpeechRuleState.SGDSActive);
        }
    }
}
