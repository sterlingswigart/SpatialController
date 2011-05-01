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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
        private ImageGenerator imageGenerator;
        private DepthGenerator depthGenerator;
        private UserGenerator userGenerator;

        private WriteableBitmap imageBitmap;
        private WriteableBitmap depthBitmap;
        private WriteableBitmap depthBitmapCorrected;
        private ImageMetaData imageData;
        private DepthMetaData depthData;

        private PoseDetectionCapability poseDetectionCapability;
        private SkeletonCapability skeletonCapability;

        private SkeletonDraw skeletonDraw;

        // TODO: Make this event-driven so it doesn't block the graphics thread.
        private SpatialController spatialController;
        private BackgroundWorker spatialWorker;

        private int[] Histogram { get; set; }

        private Thread reader;
        private BackgroundWorker worker;
        private bool stop;

        public MainWindow()
        {
            InitializeComponent();

            console = new Console();
            console.Show();
            console.Top = 0;
            console.Left = 0;

            Console.Write("TrackingNI by Richard Pianka and Ramsey Abouzahra");

            context = new Context(CONFIG_FILE);
            imageGenerator = new ImageGenerator(context);
            depthGenerator = new DepthGenerator(context);
            userGenerator = new UserGenerator(context);

            poseDetectionCapability = userGenerator.GetPoseDetectionCap();
            skeletonCapability = userGenerator.GetSkeletonCap();

            MapOutputMode mapMode = depthGenerator.GetMapOutputMode();

            int width = (int)mapMode.nXRes;
            int height = (int)mapMode.nYRes;

            imageBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            depthBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            depthBitmapCorrected = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
            imageData = new ImageMetaData();
            depthData = new DepthMetaData();

            skeletonDraw = new SkeletonDraw();

            Histogram = new int[depthGenerator.GetDeviceMaxDepth()];

            reader = new Thread(new ThreadStart(Reader));
            reader.IsBackground = true;
            worker = new BackgroundWorker();
            stop = false;

            spatialWorker = new BackgroundWorker();
            if (File.Exists(SpatialController.CALIBRATION_DATA_FILE))
            {
                spatialController = new SpatialController(ControllerStartup.FromFile, userGenerator);
            }
            else
            {
                spatialController = new SpatialController(ControllerStartup.Calibrate, userGenerator);
            }

            CompositionTarget.Rendering += new EventHandler(WorkerExec);
            Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);

            userGenerator.NewUser += new xn.UserGenerator.NewUserHandler(NewUser);
            userGenerator.LostUser += new xn.UserGenerator.LostUserHandler(LostUser);

            skeletonCapability.CalibrationStart += new SkeletonCapability.CalibrationStartHandler(CalibrationStart);
            skeletonCapability.CalibrationEnd += new SkeletonCapability.CalibrationEndHandler(CalibrationEnd);
            skeletonCapability.SetSkeletonProfile(SkeletonProfile.All);
            poseDetectionCapability.PoseDetected += new PoseDetectionCapability.PoseDetectedHandler(PoseDetected);
            poseDetectionCapability.PoseEnded += new PoseDetectionCapability.PoseEndedHandler(PoseEnded);
            reader.Start();
            worker.DoWork += new DoWorkEventHandler(WorkerTick);
            spatialWorker.DoWork += new DoWorkEventHandler(SpatialWorkerTick);
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

        private void Reader()
        {
            while (!stop)
            {
                try
                {
                    context.WaitAndUpdateAll();
                    imageGenerator.GetMetaData(imageData);
                    depthGenerator.GetMetaData(depthData);
                }
                catch (Exception) { }
            }
        }

        private void WorkerTick(object sender, DoWorkEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                imgDepth.Source = DepthImageSourceCorrected;
            });
        }
        
        private void SpatialWorkerTick(object sender, DoWorkEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                spatialController.checkGestures();
            });
        }

        private void WorkerExec(object sender, EventArgs e)
        {
            if (!worker.IsBusy)
                worker.RunWorkerAsync();

            if (!spatialWorker.IsBusy)
                spatialWorker.RunWorkerAsync(); // TODO: Performance of this may be terrible...we'll see.
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stop = true;
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
                skeletonDraw.DrawStickFigure(ref depthBitmap, depthGenerator, depthData, userGenerator);

                return depthBitmap;
            }
        }

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
                skeletonDraw.DrawStickFigure(ref depthBitmapCorrected, depthGenerator, depthData, userGenerator);

                return depthBitmapCorrected;
            }
        }
    }
}
