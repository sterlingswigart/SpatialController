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
using xn;
using System.ComponentModel;

namespace TrackingNI
{
    public partial class MainWindow : Window
    {
        private readonly string CONFIG_FILE = @"User.xml";
        private readonly int DPI_X = 96;
        private readonly int DPI_Y = 96;

        private Console console;

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

            console = new Console();
            console.Show();
            console.Top = 0;
            console.Left = 0;

            context = new Context(CONFIG_FILE);
            depthGenerator = new DepthGenerator(context);
            imageGenerator = new ImageGenerator(context);
            userGenerator = new UserGenerator(context);

            poseDetectionCapability = userGenerator.GetPoseDetectionCap();
            skeletonCapability = userGenerator.GetSkeletonCap();

            depthBitmap = new WriteableBitmap(640, 480, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            imageBitmap = new WriteableBitmap(640, 480, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            depthData = new DepthMetaData();
            imageData = new ImageMetaData();

            skeletonDraw = new SkeletonDraw();

            Device.SetUp();

            Device mock1 = new Device(new Vector3D(0, 0, 0));

            if (File.Exists(SpatialController.CALIBRATION_DATA_FILE))
            {
                spatialController = new SpatialController(ControllerStartup.FromFile, userGenerator, mock1);
            }
            else
            {
                spatialController = new SpatialController(ControllerStartup.Calibrate, userGenerator, mock1);
            }

            userGenerator.NewUser += new xn.UserGenerator.NewUserHandler(NewUser);
            userGenerator.LostUser += new xn.UserGenerator.LostUserHandler(LostUser);

            skeletonCapability.CalibrationStart += new SkeletonCapability.CalibrationStartHandler(CalibrationStart);
            skeletonCapability.CalibrationEnd += new SkeletonCapability.CalibrationEndHandler(CalibrationEnd);
            skeletonCapability.SetSkeletonProfile(SkeletonProfile.All);
            poseDetectionCapability.PoseDetected += new PoseDetectionCapability.PoseDetectedHandler(PoseDetected);
            poseDetectionCapability.PoseEnded += new PoseDetectionCapability.PoseEndedHandler(PoseEnded);

            DispatcherTimer kinectDataTimer = new DispatcherTimer();
            kinectDataTimer.Tick += new EventHandler(KinectDataTick);
            kinectDataTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            kinectDataTimer.Start();

            DispatcherTimer imageTimer = new DispatcherTimer();
            imageTimer.Tick += new EventHandler(ImageTick);
            imageTimer.Interval = new TimeSpan(0, 0, 0, 0, 60);
            imageTimer.Start();

            DispatcherTimer checkGesturesTimer = new DispatcherTimer();
            checkGesturesTimer.Tick += new EventHandler(CheckGesturesTick);
            checkGesturesTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            checkGesturesTimer.Start();

            Console.Write("Finished loading window");
        }

        private void NewUser(ProductionNode node, uint id)
        {
            userGenerator.GetPoseDetectionCap().StartPoseDetection(userGenerator.GetSkeletonCap().GetCalibrationPose(), id);
            Console.Write(id + " Found new user");
        }

        private void LostUser(ProductionNode node, uint id)
        {
            Console.Write(id + " Lost user");
        }

        private void CalibrationStart(ProductionNode node, uint id)
        {
            Console.Write(id + " Calibration start");
        }

        private void CalibrationEnd(ProductionNode node, uint id, bool success)
        {
            Console.Write(id + " Calibration ended " + (success ? "successfully" : "unsuccessfully"));
            if (success)
            {
                userGenerator.GetSkeletonCap().StartTracking(id);
            }
            else
            {
                userGenerator.GetPoseDetectionCap().StartPoseDetection(userGenerator.GetSkeletonCap().GetCalibrationPose(), id);
            }
        }

        private void PoseDetected(ProductionNode node, string pose, uint id)
        {
            Console.Write(id + " Detected pose " + pose);
            userGenerator.GetPoseDetectionCap().StopPoseDetection(id);
            userGenerator.GetSkeletonCap().RequestCalibration(id, false);
        }

        private void PoseEnded(ProductionNode node, string pose, uint id)
        {
            Console.Write(id + " Lost Pose " + pose);
        }

        private void KinectDataTick(object sender, EventArgs e)
        {
            try
            {
                //lock (spatialController.kinectDataLock)
                context.WaitAndUpdateAll();
            }
            catch (Exception) { }
        }

        private void ImageTick(object sender, EventArgs e)
        {
            //lock (spatialController.kinectDataLock)
            depthGenerator.GetMetaData(depthData);
            imageGenerator.GetMetaData(imageData);
            imgDepth.Source = RawImageSource;
            //imgDepth.Source = DepthImageSource;
        }

        private void CheckGesturesTick(object sender, EventArgs e)
        {
            //lock (spatialController.kinectDataLock)
            spatialController.checkGestures();
        }

        // thanks to Vangos Pterneas for these functions
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
                        ushort* pDepth = (ushort*)depthGenerator.GetDepthMapPtr().ToPointer();
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

                //DepthCorrection.Fix(ref depthBitmap, depthData.XRes, depthData.YRes);
                skeletonDraw.DrawStickFigure(ref depthBitmap, depthGenerator, depthData, userGenerator, spatialController.RaysToBeAnimated);

                return depthBitmap;
            }
        }
        /*
        public ImageSource DepthImageSourceCorrected
        {
            get
            {
                if (depthBitmapCorrected != null)
                {
                    UpdateHistogram(depthData);

                    depthBitmapCorrected.Lock();

                    unsafe
                    {
                        ushort* pDepth = (ushort*)depthGenerator.GetDepthMapPtr().ToPointer();
                        for (int y = 0; y < depthData.YRes; ++y)
                        {
                            byte* pDest = (byte*)depthBitmapCorrected.BackBuffer.ToPointer() + y * depthBitmapCorrected.BackBufferStride;
                            for (int x = 0; x < depthData.XRes; ++x, ++pDepth, pDest += 3)
                            {
                                byte pixel = (byte)Histogram[*pDepth];

                                pDest[0] = pixel;
                                pDest[1] = pixel;
                                pDest[2] = pixel;
                            }
                        }
                    }

                    depthBitmapCorrected.AddDirtyRect(new Int32Rect(0, 0, depthData.XRes, depthData.YRes));
                    depthBitmapCorrected.Unlock();
                }

                DepthCorrection.Fix(ref depthBitmapCorrected, depthData.XRes, depthData.YRes);
                skeletonDraw.DrawStickFigure(ref depthBitmapCorrected, depthGenerator, depthData, userGenerator, spatialController.RaysToBeAnimated);

                return depthBitmapCorrected;
            }
        }
         * */
    }
}
