/**
 * Most of this class borrowed from the TrackingNI project by
 * Richard Pianka and Abouza.
 **/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using OpenNI;
using System.ComponentModel;

namespace SpatialController
{
    public partial class MainWindow : Window
    {
        private const bool DRAW_SKELETON = true;

        // Only can track one user now for performance reasons.
        private bool trackingUser;
        private int trackingUserId;

        private readonly string CONFIG_FILE = @"User.xml";
        private readonly int DPI_X = 96;
        private readonly int DPI_Y = 96;

        private Console console;
        private UserPrompt prompt;

        private Context context;
        private DepthGenerator depthGenerator;
        private ImageGenerator imageGenerator;
        private UserGenerator userGenerator;

        private WriteableBitmap depthBitmap;
        private WriteableBitmap imageBitmap;
        private DepthMetaData depthData;
        private ImageMetaData imageData;

        private PoseDetectionCapability poseDetectionCapability;
        private SkeletonCapability skeletonCapability;

        private SpatialController spatialController;
        private SkeletonDraw skeletonDraw;

        private int[] Histogram { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            trackingUser = false;
            trackingUserId = 0;

            console = new Console();
            console.Show();
            console.Top = 10;
            console.Left = 10;

            prompt = new UserPrompt();
            console.Show();
            console.Top = 10;
            console.Left = 530;

            this.Top = 300;
            this.Left = 530;

            context = new Context(CONFIG_FILE);
            imageGenerator = new ImageGenerator(context);
            userGenerator = new UserGenerator(context);
            
            if (DRAW_SKELETON)
            {
                depthGenerator = new DepthGenerator(context);
                depthBitmap = new WriteableBitmap(640, 480, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
                depthData = new DepthMetaData();
                Histogram = new int[depthGenerator.DeviceMaxDepth];
                skeletonDraw = new SkeletonDraw();
            }

            poseDetectionCapability = userGenerator.PoseDetectionCapability;
            skeletonCapability = userGenerator.SkeletonCapability;
            
            imageBitmap = new WriteableBitmap(640, 480, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            imageData = new ImageMetaData();

            Device.SetUp();

            Device mock1 = new Device(new Vector3D(0, 0, 0), 0);

            if (File.Exists(SpatialController.CALIBRATION_DATA_FILE))
            {
                spatialController = new SpatialController(ControllerStartup.FromFile, userGenerator, mock1);
            }
            else
            {
                spatialController = new SpatialController(ControllerStartup.Calibrate, userGenerator, mock1);
            }

            userGenerator.NewUser += NewUser;
            userGenerator.LostUser += LostUser;
            
            skeletonCapability.CalibrationStart += CalibrationStart;
            skeletonCapability.CalibrationEnd += CalibrationEnd;
            skeletonCapability.SetSkeletonProfile(SkeletonProfile.All);
            poseDetectionCapability.PoseDetected += PoseDetected;
            poseDetectionCapability.PoseEnded += PoseEnded;

            DispatcherTimer kinectDataTimer = new DispatcherTimer();
            kinectDataTimer.Tick += new EventHandler(KinectDataTick);
            kinectDataTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            kinectDataTimer.Start();

            DispatcherTimer imageTimer = new DispatcherTimer();
            imageTimer.Tick += new EventHandler(ImageTick);
            imageTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            imageTimer.Start();

            DispatcherTimer checkGesturesTimer = new DispatcherTimer();
            checkGesturesTimer.Tick += new EventHandler(CheckGesturesTick);
            checkGesturesTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            checkGesturesTimer.Start();

            UserPrompt.Write("Finished loading window.");
            Console.Write("Finished loading window");

            UserPrompt.Write("Please assume the Psi pose and hold it until you see a skeleton overlaid"
                    + " on the streaming video.");
        }

        private void NewUser(object sender, NewUserEventArgs e)
        {
            userGenerator.PoseDetectionCapability.StartPoseDetection(userGenerator.SkeletonCapability.CalibrationPose, e.ID);
            Console.Write(e.ID + " Found new user");
        }

        private void LostUser(object sender, UserLostEventArgs e)
        {
            if (trackingUserId == e.ID)
                trackingUser = false;
            Console.Write(e.ID + " Lost user");
        }

        private void CalibrationStart(object sender, CalibrationStartEventArgs e)
        {
            Console.Write(e.ID + " Calibration start");
        }

        private void CalibrationEnd(object sender, CalibrationEndEventArgs e)
        {
            Console.Write(e.ID + " Calibration ended " + (e.Success ? "successfully" : "unsuccessfully"));
            if (e.Success)
            {
                if (trackingUser)
                    userGenerator.SkeletonCapability.StopTracking(trackingUserId);
                userGenerator.SkeletonCapability.StartTracking(e.ID);
                trackingUser = true;
                trackingUserId = e.ID;
            }
            else
            {
                userGenerator.PoseDetectionCapability.StartPoseDetection(userGenerator.SkeletonCapability.CalibrationPose, e.ID);
            }
        }

        private void PoseDetected(object sender, PoseDetectedEventArgs e)
        {
            Console.Write(e.ID + " Detected pose " + e.Pose);
            userGenerator.PoseDetectionCapability.StopPoseDetection(e.ID);
            userGenerator.SkeletonCapability.RequestCalibration(e.ID, false);
        }

        private void PoseEnded(object sender, PoseEndedEventArgs e)
        {
            Console.Write(e.ID + " Lost Pose " + e.Pose);
        }

        private void KinectDataTick(object sender, EventArgs e)
        {
            try
            {
                context.WaitAndUpdateAll();
                imageGenerator.GetMetaData(imageData);
                //depthGenerator.GetMetaData(depthData);
            }
            catch (Exception) { }
        }

        private void ImageTick(object sender, EventArgs e)
        {
            //imgDepth.Source = DepthImageSource;
            if (imageData != null && imageData.DataSize > 1)
                imgDepth.Source = RawImageSource;
        }

        private void CheckGesturesTick(object sender, EventArgs e)
        {
            spatialController.checkGestures();
        }

        // Thanks to Vangos Pterneas for these functions.
        public unsafe void UpdateHistogram(DepthMetaData depthMD)
        {
            for (int i = 0; i < Histogram.Length; ++i)
                Histogram[i] = 0;

            ushort* pDepth = (ushort*)depthMD.DepthMapPtr.ToPointer();

            int points = 0;
            for (int y = 0; y < depthMD.YRes; ++y)
            {
                for (int x = 0; x < depthMD.XRes; ++x, ++pDepth)
                {
                    ushort depthVal = *pDepth;
                    if (depthVal != 0)
                    {
                        Histogram[depthVal]++;
                        points++;
                    }
                }
            }

            for (int i = 1; i < Histogram.Length; i++)
            {
                Histogram[i] += Histogram[i - 1];
            }

            if (points > 0)
            {
                for (int i = 1; i < Histogram.Length; i++)
                {
                    Histogram[i] = (int)(256 * (1.0f - (Histogram[i] / (float)points)));
                }
            }
        }

        public ImageSource RawImageSource
        {
            get
            {
                if (imageBitmap != null)
                {
                    imageBitmap.Lock();
                    imageBitmap.WritePixels(new Int32Rect(0, 0, imageData.XRes, imageData.YRes), imageData.ImageMapPtr, (int)imageData.DataSize, imageBitmap.BackBufferStride);
                    imageBitmap.Unlock();
                }

                if (DRAW_SKELETON)
                    skeletonDraw.DrawStickFigure(ref imageBitmap, depthGenerator, depthData, userGenerator, spatialController.RaysToBeAnimated);
                return imageBitmap;
            }
        }
        
        public ImageSource DepthImageSource
        {
            get
            {
                if (depthBitmap != null)
                {
                    UpdateHistogram(depthData);

                    depthBitmap.Lock();

                    unsafe
                    {
                        ushort* pDepth = (ushort*)depthGenerator.DepthMapPtr.ToPointer();
                        for (int y = 0; y < depthData.YRes; ++y)
                        {
                            byte* pDest = (byte*)depthBitmap.BackBuffer.ToPointer() + y * depthBitmap.BackBufferStride;
                            for (int x = 0; x < depthData.XRes; ++x, ++pDepth, pDest += 3)
                            {
                                byte pixel = (byte)Histogram[*pDepth];

                                pDest[0] = pixel;
                                pDest[1] = pixel;
                                pDest[2] = pixel;
                            }
                        }
                    }

                    depthBitmap.AddDirtyRect(new Int32Rect(0, 0, depthData.XRes, depthData.YRes));
                    depthBitmap.Unlock();
                }

                DepthCorrection.Fix(ref depthBitmap, depthData.XRes, depthData.YRes);
                skeletonDraw.DrawStickFigure(ref depthBitmap, depthGenerator, depthData, userGenerator, spatialController.RaysToBeAnimated);

                return depthBitmap;
            }
        }
    }
}
